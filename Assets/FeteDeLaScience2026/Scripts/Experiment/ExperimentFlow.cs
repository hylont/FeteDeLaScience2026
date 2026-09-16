using EditorAttributes;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Drives the full trial from calibration to the final fade to black:
//   1) Left controller X calibrates the rig once both hands are seen resting on the table.
//   2) A coherent piece is placed, then an incoherent one, in that fixed order.
//   3) The cake follows the pinching hand while the left grip is held (see CakePiece).
//   4) Scent is diffused once the (held) piece gets close to the mouth.
//   5) The experimenter asks the two questions out loud and notes them on paper.
//   6) Left controller X (once the scent has been diffused) starts the next trial, or ends it.
public class ExperimentFlow : MonoBehaviour
{
    private enum EState
    {
        WaitingCalibration,
        TrialInProgress,
        Ended
    }


    [SerializeField]
    ScentDiffusionParameters _testScent = new(1, .5f, 3000);

    [Button("Diffuse test")]
    void DiffuseTest()
    {
        if(_scentDiffuser != null) _scentDiffuser.RequestDiffusion(_testScent);
    }

    private const int TotalTrials = 2;

    [Header("Calibration")]
    [SerializeField] bool _disableCalibration = false;
    [SerializeField] private CalibrationRig _calibrationRig;

    [Header("Chef")]
    [SerializeField] private Animator _chefAnimator;
    [SerializeField] private Transform _cakeServingPoint;

    [Header("Cake")]
    [SerializeField] private CakePiece _cakePiece;
    [SerializeField] private Transform _mouthReference;
    [SerializeField] private float _mouthProximityThreshold = 0.12f;
    [SerializeField] private float _cakePlacementDelay = 1f;

    [Header("Scent")]
    [Tooltip("Component implementing IScentDiffuser, e.g. the Olfy prefab's OlfyHandler.")]
    [SerializeField] private MonoBehaviour _scentDiffuserBehaviour;
    [SerializeField] [Range(0f, 1f)] private float _scentStrength = 0.7f;
    [SerializeField] [Range(1000, 10000)] private int _scentDuration = 3000;

    [Header("Flavors")]
    [SerializeField]
    private FlavorDefinition[] _flavors =
    {
        new FlavorDefinition(EFlavor.Chocolat, new Color(0.36f, 0.20f, 0.09f), 1),
        new FlavorDefinition(EFlavor.Citron, Color.yellow, 2),
        new FlavorDefinition(EFlavor.Fraise, Color.red, 3)
    };

    [Header("End")]
    [SerializeField] private ScreenFader _screenFader;

    [Header("Debug")]
    [Tooltip("Plain-language readout of the current state and expected interaction, for the experimenter.")]
    [SerializeField] private TextMeshProUGUI _debugText;

    private static readonly int PlacingTrigger = Animator.StringToHash("PLACING");

    private IScentDiffuser _scentDiffuser;

    [ShowInInspector] private EState _state = EState.WaitingCalibration;
    private int _trialIndex;
    private TrialData _currentTrial;
    private bool _scentDiffusedThisTrial;

    private void Awake()
    {
        _scentDiffuser = _scentDiffuserBehaviour as IScentDiffuser;
        if (_scentDiffuser == null)
        {
            LLogger.E("Scent diffuser reference does not implement IScentDiffuser.");
        }
    }

    private void Start()
    {
        _cakePiece.gameObject.SetActive(false);
        ValidateFlavorDefinitions();
        UpdateDebugText();
    }

    private void Update()
    {
        bool confirmPressed = OVRInput.GetDown(OVRInput.RawButton.X, OVRInput.Controller.LTouch)
            || OVRInput.GetDown(OVRInput.RawButton.A, OVRInput.Controller.RTouch)
            || Keyboard.current.enterKey.wasPressedThisFrame;

        if(OVRInput.GetDown(OVRInput.RawButton.B, OVRInput.Controller.RTouch)) DiffuseTest();

        switch (_state)
        {
            case EState.WaitingCalibration:
                if (confirmPressed) TryCalibrate();
                break;

            case EState.TrialInProgress:
                UpdateTrial();
                if (_scentDiffusedThisTrial && confirmPressed) AdvanceTrial();
                break;
        }

        UpdateDebugText();
    }

    private void TryCalibrate()
    {
        if(_disableCalibration)
        {
            BeginTrial(TrialData.GenerateCoherent());
        }
        else
        {
            _calibrationRig.TryCalibrate();
            StartCoroutine(StartTrial_Coroutine(TrialData.GenerateCoherent()));
        }
    }

    IEnumerator StartTrial_Coroutine(TrialData trial)
    {
        if(_screenFader != null) yield return new WaitForSeconds(_screenFader.FadeDuration);
        BeginTrial(trial);
    }

    private void BeginTrial(TrialData trial)
    {
        _currentTrial = trial;
        _trialIndex++;
        _scentDiffusedThisTrial = false;

        if (_chefAnimator != null) _chefAnimator.SetTrigger(PlacingTrigger);

        FlavorDefinition colorDef = GetDefinition(trial.ColorFlavor);
        _cakePiece.ResetToServingPoint(_cakeServingPoint);
        _cakePiece.SetColor(colorDef.Color);

        StartCoroutine(EnableCakePiece_Coroutine());

        _state = EState.TrialInProgress;

        LLogger.L($"Trial {_trialIndex}/{TotalTrials} start — condition={trial.Condition}, color={trial.ColorFlavor}, scent={trial.ScentFlavor}");
    }

    IEnumerator EnableCakePiece_Coroutine()
    {
        yield return new WaitForSeconds(_cakePlacementDelay);
        _cakePiece.gameObject.SetActive(true);
    }

    private void UpdateTrial()
    {
        if (_scentDiffusedThisTrial || !_cakePiece.IsBeingHeld || _mouthReference == null) return;

        float dist = Vector3.Distance(_cakePiece.transform.position, _mouthReference.position);
        if (dist <= _mouthProximityThreshold)
        {
            DiffuseScent();
        }
    }

    private void DiffuseScent()
    {
        _scentDiffusedThisTrial = true;
        _cakePiece.StopFollowing();
        _cakePiece.gameObject.SetActive(false);

        FlavorDefinition scentDef = GetDefinition(_currentTrial.ScentFlavor);
        var parameters = new ScentDiffusionParameters(scentDef.ScentSlot, _scentStrength, _scentDuration);
        bool sent = _scentDiffuser != null && _scentDiffuser.RequestDiffusion(parameters);

        LLogger.L($"Trial {_trialIndex} scent diffused — flavor={_currentTrial.ScentFlavor}, slot={scentDef.ScentSlot}, sent={sent}");
    }

    private void AdvanceTrial()
    {
        LLogger.L($"Trial {_trialIndex} ended by experimenter.");

        if (_trialIndex >= TotalTrials)
        {
            EndExperience();
        }
        else
        {
            BeginTrial(TrialData.GenerateIncoherent());
        }
    }

    private void EndExperience()
    {
        _state = EState.Ended;
        LLogger.L("Experience ended.");
        if (_screenFader != null) _screenFader.FadeToBlack();
    }

    private FlavorDefinition GetDefinition(EFlavor flavor)
    {
        foreach (FlavorDefinition def in _flavors)
        {
            if (def.Flavor == flavor) return def;
        }

        LLogger.E($"No FlavorDefinition configured for {flavor}; using fallback.");
        return new FlavorDefinition(flavor, Color.white, 1);
    }

    private void ValidateFlavorDefinitions()
    {
        foreach (EFlavor flavor in System.Enum.GetValues(typeof(EFlavor)))
        {
            bool found = false;
            foreach (FlavorDefinition def in _flavors)
            {
                if (def.Flavor == flavor) { found = true; break; }
            }

            if (!found) LLogger.E($"Missing FlavorDefinition for {flavor}.");
        }
    }

    private void UpdateDebugText()
    {
        if (_debugText == null) return;
        _debugText.text = BuildDebugMessage();
    }

    private string BuildDebugMessage()
    {
        switch (_state)
        {
            case EState.WaitingCalibration:
                return "CALIBRATION\n"
                     + "Le participant pose ses deux mains à plat sur la table.\n"
                     + "Appuyez sur X (manette gauche) pour calibrer la hauteur et l'orientation.";

            case EState.TrialInProgress:
                return BuildTrialMessage();

            case EState.Ended:
                return "EXPÉRIENCE TERMINÉE\nÉcran noir.";

            default:
                return "";
        }
    }

    private string BuildTrialMessage()
    {
        string condition = _currentTrial.Condition == ETrialCondition.Coherent ? "COHÉRENT" : "INCOHÉRENT";
        string header = $"ESSAI {_trialIndex}/{TotalTrials} — {condition}\n"
                       + $"Couleur montrée : {_currentTrial.ColorFlavor}   |   Odeur réelle : {_currentTrial.ScentFlavor}\n\n";

        if (_scentDiffusedThisTrial)
        {
            return header
                 + "Odeur diffusée.\n"
                 + "Demandez à l'oral : « Quel gâteau avez-vous mangé ? »\n"
                 + "puis : « À quel point l'odeur était forte, de 0 à 10 ? »\n"
                 + "Notez les 2 réponses sur la feuille, puis appuyez sur X pour continuer.";
        }

        if (_cakePiece.IsBeingHeld)
        {
            return header
                 + "Morceau virtuel attaché à la main.\n"
                 + "Approchez-le de la bouche du participant pour déclencher l'odeur.";
        }

        return header
             + "Le chef dépose le morceau.\n"
             + "Dès que le participant attrape le vrai morceau, maintenez la gâchette\n"
             + "latérale gauche (grip) pour attacher le morceau virtuel à sa main.";
    }
}
