using EditorAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Drives one session (= one scene load = one participant) from calibration to the final fade to black:
//   1) Left controller X / right controller A calibrates the rig once both hands rest on the table.
//   2) COH trial, then INC trial. Each time the cake appears on the plate, follows the pinching hand
//      while the grip is held (see CakePiece), and the scent is diffused once it gets close to the mouth.
//   3) A few seconds later the participant answers by grabbing answer spheres (see GrabbableAnswer):
//      COH: taste, perceived odor, then intensity (depending on the odor answer). INC: taste only.
//   4) Every answer is saved to persistentDataPath/Results as JSON, with a random participant ID.
public class ExperimentFlow : MonoBehaviour
{
    private enum EState
    {
        WaitingCalibration,
        Calibrating,
        Tasting,
        Answering,
        Ended
    }

    private enum EDiffusionTiming
    {
        OnMouthProximity,
        OnTrialStart,
        OnCakeHeld
    }

    [Serializable]
    public class Question
    {
        public string Text;
        [Tooltip("Parent of this question's GrabbableAnswer spheres, only shown while the question is asked.")]
        public GameObject Answers;
    }

    // Saved results. Strings rather than enums, since JsonUtility writes enums as numbers.
    [Serializable] private class AnswerRecord { public string Question, Answer, Time; }
    [Serializable] private class TrialRecord { public string Condition, ColorFlavor, ScentFlavor; public bool ScentSent; public List<AnswerRecord> Answers = new(); }
    [Serializable] private class SessionRecord { public string ParticipantId, StartTime; public int ScentIntensity; public List<TrialRecord> Trials = new(); }

    [SerializeField] bool _allowTesting = false;
    [SerializeField]
    ScentDiffusionParameters _testScent = new(1, .5f, 3000);

    [Button("Diffuse test")]
    void DiffuseTest()
    {
        if(_scentDiffuser != null) _scentDiffuser.RequestDiffusion(_testScent);
    }

    private const int TotalTrials = 2;

    // Hierarchy index of "Il y avait une odeur" under _smellQuestion.Answers: this answer skips the
    // intensity question, the two others ("pas d'odeur", "pas sûr") lead to it.
    private const int OdorPresentAnswerIndex = 2;

    [Header("Calibration")]
    [SerializeField] bool _disableCalibration = false;
    [SerializeField] private CalibrationRig _calibrationRig;
    [SerializeField] AudioSource _calibrationSuccessfulClip;

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
    [SerializeField] [Range(1000, 10000)] private int _scentDuration = 3000;
    [SerializeField] private EDiffusionTiming _diffuseAtTiming = EDiffusionTiming.OnMouthProximity;

    [Header("Flavors")]
    [SerializeField]
    private FlavorDefinition[] _flavors =
    {
        new FlavorDefinition(EFlavor.Chocolat, new Color(0.36f, 0.20f, 0.09f), 1),
        new FlavorDefinition(EFlavor.Citron, Color.yellow, 2),
        new FlavorDefinition(EFlavor.Fraise, Color.red, 3)
    };

    [Header("Questions")]
    [Tooltip("World-space text showing the current question to the participant.")]
    [SerializeField] private TMP_Text _questionText;
    [Tooltip("Seconds between the cake reaching the mouth and the first question.")]
    [SerializeField] private float _answersDelay = 3f;
    [SerializeField] private Question _tasteQuestion = new() { Text = "De quel goût était le gâteau ?" };
    [SerializeField] private Question _smellQuestion = new() { Text = "Avez-vous senti une odeur ?" };
    [SerializeField] private Question _intensityQuestion = new() { Text = "Est-ce que l'odeur était trop forte ?" };

    [Header("End")]
    [SerializeField] private ScreenFader _screenFader;

    [Header("Debug")]
    [SerializeField] float _resetButtonHoldDuration = 3f;
    float _resetButtonHeld = 0f;

    [Tooltip("Plain-language readout of the current state and expected interaction, for the experimenter.")]
    [SerializeField] private TextMeshProUGUI _debugText;

    private static readonly int PlacingTrigger = Animator.StringToHash("PLACING");

    private IScentDiffuser _scentDiffuser;

    [ShowInInspector] private EState _state = EState.WaitingCalibration;
    private SessionRecord _session;
    private string _resultsPath;
    private TrialData _currentTrial;
    private bool _scentDiffusedThisTrial;
    private Question _currentQuestion;
    private GrabbableAnswer _grabbedAnswer;
    private readonly List<EFlavor> _usedColorFlavors = new();
    private readonly List<EFlavor> _usedScentFlavors = new();

    private Question[] Questions => new[] { _tasteQuestion, _smellQuestion, _intensityQuestion };
    private TrialRecord CurrentRecord => _session.Trials[^1];

    private void Awake()
    {
        // OlfyHandler persists across scene reloads (DontDestroyOnLoad), but this
        // serialized reference gets re-linked to a fresh throwaway copy of the Olfy
        // prefab on every reload, so prefer the persisted singleton when it exists.
        _scentDiffuser = OlfyHandler.Instance != null
            ? OlfyHandler.Instance
            : _scentDiffuserBehaviour as IScentDiffuser;

        if (_scentDiffuser == null)
        {
            LLogger.E("Scent diffuser reference does not implement IScentDiffuser.");
        }

        // Each scene load is a new session: new participant ID and a scent intensity (10..100%) for the whole experience.
        DateTime now = DateTime.Now;
        _session = new SessionRecord
        {
            ParticipantId = Guid.NewGuid().ToString("N").Substring(0, 8),
            StartTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
            ScentIntensity = UnityEngine.Random.Range(1, 11) * 10
        };
        _resultsPath = Path.Combine(Application.persistentDataPath, "Results", $"{now:yyyyMMdd_HHmmss}_{_session.ParticipantId}.json");

        LLogger.L($"Session {_session.ParticipantId} — scent intensity {_session.ScentIntensity}%");
    }

    private void Start()
    {
        _cakePiece.gameObject.SetActive(false);

        foreach (Question question in Questions)
        {
            foreach (GrabbableAnswer answer in question.Answers.GetComponentsInChildren<GrabbableAnswer>(true))
            {
                answer.Grabbed += OnAnswerGrabbed;
            }
        }
        ShowQuestion(null);

        ValidateFlavorDefinitions();
        UpdateDebugText();
    }

    private void Update()
    {
        bool confirmPressed = OVRInput.GetDown(OVRInput.RawButton.X, OVRInput.Controller.LTouch)
            || OVRInput.GetDown(OVRInput.RawButton.A, OVRInput.Controller.RTouch)
            || Keyboard.current.enterKey.wasPressedThisFrame;

        if(_allowTesting &&
            OVRInput.GetDown(OVRInput.RawButton.B, OVRInput.Controller.RTouch)) DiffuseTest();

        if(OVRInput.Get(OVRInput.RawButton.RIndexTrigger, OVRInput.Controller.RTouch)
            || OVRInput.Get(OVRInput.RawButton.LIndexTrigger, OVRInput.Controller.LTouch))
        {
            _resetButtonHeld += Time.deltaTime;

            if(_resetButtonHeld >= _resetButtonHoldDuration)
            {
                LLogger.L("Resetting experiment.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        else
        {
            _resetButtonHeld = 0f;
        }

        if (_state == EState.WaitingCalibration && confirmPressed)
        {
            StartCoroutine(RunExperiment());
        }

        UpdateDebugText();
    }

    private IEnumerator RunExperiment()
    {
        _state = EState.Calibrating;
        if (!_disableCalibration)
        {
            _calibrationRig.TryCalibrate();
            if(_calibrationSuccessfulClip != null) _calibrationSuccessfulClip.Play();
            if (_screenFader != null) yield return new WaitForSeconds(_screenFader.FadeDuration);
        }

        yield return RunTasting(TrialData.GenerateCoherent(_usedColorFlavors, _usedScentFlavors));
        yield return Ask(_tasteQuestion);
        yield return Ask(_smellQuestion);
        if (_grabbedAnswer.transform.GetSiblingIndex() == OdorPresentAnswerIndex)
        {
            yield return Ask(_intensityQuestion);
        }

        yield return RunTasting(TrialData.GenerateIncoherent(_usedColorFlavors, _usedScentFlavors));
        yield return Ask(_tasteQuestion);

        EndExperience();
    }

    // Serves the cake, diffuses the scent and waits until the piece has been "eaten".
    private IEnumerator RunTasting(TrialData trial)
    {
        _state = EState.Tasting;
        _currentTrial = trial;
        _scentDiffusedThisTrial = false;
        _usedColorFlavors.Add(trial.ColorFlavor);
        _usedScentFlavors.Add(trial.ScentFlavor);
        _session.Trials.Add(new TrialRecord
        {
            Condition = ConditionLabel(trial),
            ColorFlavor = trial.ColorFlavor.ToString(),
            ScentFlavor = trial.ScentFlavor.ToString()
        });

        if (_chefAnimator != null) _chefAnimator.SetTrigger(PlacingTrigger);
        _cakePiece.ResetToServingPoint(_cakeServingPoint);
        _cakePiece.SetColor(GetDefinition(trial.ColorFlavor).Color);

        LLogger.L($"Trial {_session.Trials.Count}/{TotalTrials} start — condition={trial.Condition}, color={trial.ColorFlavor}, scent={trial.ScentFlavor}");

        if(_questionText) _questionText.text = "Vous pouvez attraper et manger le gâteau";

        yield return new WaitForSeconds(_cakePlacementDelay);
        _cakePiece.gameObject.SetActive(true);

        yield return new WaitUntil(ShouldDiffuse);
        DiffuseScent();

        yield return new WaitUntil(CakeIsNearMouth);
        _cakePiece.StopFollowing();
        _cakePiece.gameObject.SetActive(false);

        yield return new WaitForSeconds(_answersDelay);
    }

    // Shows a question and waits for the participant to grab one of its answers, then saves it.
    private IEnumerator Ask(Question question)
    {
        _state = EState.Answering;
        _grabbedAnswer = null;
        ShowQuestion(question);

        yield return new WaitUntil(() => _grabbedAnswer != null);

        ShowQuestion(null);
        CurrentRecord.Answers.Add(new AnswerRecord
        {
            Question = question.Text,
            Answer = _grabbedAnswer.Label,
            Time = DateTime.Now.ToString("HH:mm:ss")
        });
        SaveResults();

        LLogger.L($"Answer — {question.Text} {_grabbedAnswer.Label}");
    }

    private void OnAnswerGrabbed(GrabbableAnswer answer)
    {
        // Only the first grab counts until the next question is asked.
        if (_grabbedAnswer == null) _grabbedAnswer = answer;
    }

    private void ShowQuestion(Question question)
    {
        _currentQuestion = question;
        foreach (Question q in Questions) q.Answers.SetActive(q == question);

        if (_questionText == null) return;
        _questionText.text = question?.Text ?? "";
        _questionText.gameObject.SetActive(question != null);
    }

    private bool ShouldDiffuse() => _diffuseAtTiming switch
    {
        EDiffusionTiming.OnTrialStart => true,
        EDiffusionTiming.OnCakeHeld => _cakePiece.IsBeingHeld,
        _ => _cakePiece.IsBeingHeld && CakeIsNearMouth()
    };

    private bool CakeIsNearMouth()
    {
        return Vector3.Distance(_cakePiece.transform.position, _mouthReference.position) <= _mouthProximityThreshold;
    }

    private void DiffuseScent()
    {
        _scentDiffusedThisTrial = true;

        FlavorDefinition scentDef = GetDefinition(_currentTrial.ScentFlavor);
        var parameters = new ScentDiffusionParameters(scentDef.ScentSlot, _session.ScentIntensity / 100f, _scentDuration);
        bool sent = _scentDiffuser != null && _scentDiffuser.RequestDiffusion(parameters);
        CurrentRecord.ScentSent = sent;

        LLogger.L($"Trial {_session.Trials.Count} scent diffused — flavor={_currentTrial.ScentFlavor}, slot={scentDef.ScentSlot}, intensity={_session.ScentIntensity}%, sent={sent}");
    }

    private void EndExperience()
    {
        _state = EState.Ended;
        LLogger.L($"Experience ended. Results saved to {_resultsPath}");
        if (_screenFader != null) _screenFader.FadeToBlack();
    }

    private void SaveResults()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_resultsPath));
            File.WriteAllText(_resultsPath, JsonUtility.ToJson(_session, true));
        }
        catch (Exception e)
        {
            LLogger.E($"Could not save results to {_resultsPath}: {e.Message}");
        }
    }

    private static string ConditionLabel(TrialData trial) => trial.Condition == ETrialCondition.Coherent ? "COH" : "INC";

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
        foreach (EFlavor flavor in Enum.GetValues(typeof(EFlavor)))
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

            case EState.Calibrating:
                return "CALIBRATION EN COURS...";

            case EState.Tasting:
                return BuildTrialHeader() + BuildTastingMessage();

            case EState.Answering:
                return BuildTrialHeader()
                     + $"Question : « {_currentQuestion?.Text} »\n"
                     + "Le participant attrape une sphère pour répondre.";

            case EState.Ended:
                return "EXPÉRIENCE TERMINÉE\nÉcran noir.\n"
                     + $"Résultats : {_resultsPath}";

            default:
                return "";
        }
    }

    private string BuildTrialHeader()
    {
        return $"ESSAI {_session.Trials.Count}/{TotalTrials} — {ConditionLabel(_currentTrial)}   |   Participant {_session.ParticipantId}   |   Intensité {_session.ScentIntensity}%\n"
             + $"Couleur montrée : {_currentTrial.ColorFlavor}   |   Odeur réelle : {_currentTrial.ScentFlavor}\n\n";
    }

    private string BuildTastingMessage()
    {
        if (_scentDiffusedThisTrial)
        {
            return "Odeur diffusée.\n"
                 + "Les questions apparaissent quelques secondes après que le morceau a atteint la bouche.";
        }

        if (_cakePiece.IsBeingHeld)
        {
            return "Morceau virtuel attaché à la main.\n"
                 + "Approchez-le de la bouche du participant pour déclencher l'odeur.";
        }

        return "Le chef dépose le morceau.\n"
             + "Dès que le participant attrape le vrai morceau, maintenez la gâchette\n"
             + "latérale (grip) pour attacher le morceau virtuel à sa main.";
    }
}
