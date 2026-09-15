using UnityEngine;
using UnityEngine.InputSystem;

// Drives the full trial from calibration to the final fade to black:
//   1) Space calibrates the rig once both hands are seen resting on the table.
//   2) A coherent piece is placed, then an incoherent one, in that fixed order.
//   3) The cake follows the pinching hand while Ctrl is held (see CakePiece).
//   4) Scent is diffused once the (held) piece gets close to the mouth.
//   5) The 3 flavor buttons appear right after the scent is diffused.
//   6) Answering reveals the intensity slider.
//   7) Space (while the slider is up) logs the rating and starts the next trial, or ends it.
public class ExperimentFlow : MonoBehaviour
{
    private enum EState
    {
        WaitingCalibration,
        TrialInProgress,
        Ended
    }

    private const int TotalTrials = 2;

    [Header("Calibration")]
    [SerializeField] private CalibrationRig _calibrationRig;

    [Header("Chef")]
    [SerializeField] private Animator _chefAnimator;
    [SerializeField] private Transform _cakeServingPoint;

    [Header("Cake")]
    [SerializeField] private CakePiece _cakePiece;
    [SerializeField] private Transform _mouthReference;
    [SerializeField] private float _mouthProximityThreshold = 0.12f;

    [Header("Scent")]
    [Tooltip("Component implementing IScentDiffuser, e.g. the Olfy prefab's OlfyHandler.")]
    [SerializeField] private MonoBehaviour _scentDiffuserBehaviour;
    [SerializeField] private float _scentStrength = 0.7f;
    [SerializeField] private float _scentDuration = 3f;

    [Header("Flavors")]
    [SerializeField]
    private FlavorDefinition[] _flavors =
    {
        new FlavorDefinition(EFlavor.Chocolat, new Color(0.36f, 0.20f, 0.09f), 1),
        new FlavorDefinition(EFlavor.Citron, Color.yellow, 2),
        new FlavorDefinition(EFlavor.Fraise, Color.red, 3)
    };

    [Header("UI")]
    [SerializeField] private ChoiceButtonsController _choiceButtons;
    [SerializeField] private HandGrabSlider1D _intensitySlider;

    [Header("End")]
    [SerializeField] private ScreenFader _screenFader;

    private static readonly int PlacingTrigger = Animator.StringToHash("PLACING");

    private IScentDiffuser _scentDiffuser;
    private EState _state = EState.WaitingCalibration;
    private int _trialIndex;
    private TrialData _currentTrial;
    private bool _scentDiffusedThisTrial;
    private bool _awaitingIntensityConfirm;

    private void Awake()
    {
        _scentDiffuser = _scentDiffuserBehaviour as IScentDiffuser;
        if (_scentDiffuser == null)
        {
            LLogger.E("Scent diffuser reference does not implement IScentDiffuser.");
        }

        _choiceButtons.Chosen += OnFlavorChosen;
    }

    private void Start()
    {
        _cakePiece.gameObject.SetActive(false);
        _intensitySlider.Hide();
        ValidateFlavorDefinitions();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool spacePressed = kb != null && kb.spaceKey.wasPressedThisFrame;

        switch (_state)
        {
            case EState.WaitingCalibration:
                if (spacePressed) TryCalibrate();
                break;

            case EState.TrialInProgress:
                UpdateTrial();
                if (spacePressed) TryAdvanceFromIntensity();
                break;
        }
    }

    private void TryCalibrate()
    {
        if (_calibrationRig.TryCalibrate())
        {
            BeginTrial(TrialData.GenerateCoherent());
        }
    }

    private void BeginTrial(TrialData trial)
    {
        _currentTrial = trial;
        _trialIndex++;
        _scentDiffusedThisTrial = false;
        _awaitingIntensityConfirm = false;

        FlavorDefinition colorDef = GetDefinition(trial.ColorFlavor);
        _cakePiece.gameObject.SetActive(true);
        _cakePiece.ResetToServingPoint(_cakeServingPoint);
        _cakePiece.SetColor(colorDef.Color);

        _choiceButtons.Hide();
        _intensitySlider.Hide();

        if (_chefAnimator != null) _chefAnimator.SetTrigger(PlacingTrigger);

        _state = EState.TrialInProgress;

        LLogger.L($"Trial {_trialIndex}/{TotalTrials} start — condition={trial.Condition}, color={trial.ColorFlavor}, scent={trial.ScentFlavor}");
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
        FlavorDefinition scentDef = GetDefinition(_currentTrial.ScentFlavor);
        var parameters = new ScentDiffusionParameters(scentDef.ScentSlot, _scentStrength, _scentDuration);
        bool sent = _scentDiffuser != null && _scentDiffuser.RequestDiffusion(parameters);

        LLogger.L($"Trial {_trialIndex} scent diffused — flavor={_currentTrial.ScentFlavor}, slot={scentDef.ScentSlot}, sent={sent}");

        _choiceButtons.Show();
    }

    private void OnFlavorChosen(EFlavor flavor)
    {
        LLogger.L($"Trial {_trialIndex} answer — chosen={flavor} (color was {_currentTrial.ColorFlavor}, scent was {_currentTrial.ScentFlavor})");

        _intensitySlider.Show();
        _awaitingIntensityConfirm = true;
    }

    private void TryAdvanceFromIntensity()
    {
        if (!_awaitingIntensityConfirm) return;

        LLogger.L($"Trial {_trialIndex} intensity confirmed — value={_intensitySlider.Value}");

        _awaitingIntensityConfirm = false;
        _intensitySlider.Hide();

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
}
