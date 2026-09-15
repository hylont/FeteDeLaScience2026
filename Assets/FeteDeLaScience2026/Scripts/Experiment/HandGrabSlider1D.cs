using TMPro;
using UnityEngine;

// A physical slider handle that can be pinched and dragged along a rail with one hand.
// Value is read by ExperimentFlow when the experimenter confirms the trial (Space).
public class HandGrabSlider1D : MonoBehaviour
{
    [Header("Rail")]
    [SerializeField] private Transform _handle;
    [SerializeField] private Transform _railStart; // Value == _minValue
    [SerializeField] private Transform _railEnd;    // Value == _maxValue
    [SerializeField] private float _grabRadius = 0.05f;

    [Header("Value")]
    [SerializeField] private int _minValue = 0;
    [SerializeField] private int _maxValue = 10;
    [SerializeField] private TextMeshProUGUI _valueText;

    public int Value { get; private set; }

    private OVRHand _grabbingHand;

    private void OnEnable()
    {
        _grabbingHand = null;
        SetNormalized(0f);
    }

    private void Update()
    {
        if (_grabbingHand == null)
        {
            _grabbingHand = FindNewGrab();
        }
        else if (!IsHandStillPinching(_grabbingHand))
        {
            _grabbingHand = null;
        }

        if (_grabbingHand != null)
        {
            FollowHand(_grabbingHand);
        }
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    private OVRHand FindNewGrab()
    {
        OVRHand left = ExperimentHands.Instance.Left;
        OVRHand right = ExperimentHands.Instance.Right;

        if (IsHandGrabbingHandle(left)) return left;
        if (IsHandGrabbingHandle(right)) return right;
        return null;
    }

    private bool IsHandGrabbingHandle(OVRHand hand)
    {
        return IsHandStillPinching(hand) &&
               Vector3.Distance(hand.PointerPose.position, _handle.position) <= _grabRadius;
    }

    private static bool IsHandStillPinching(OVRHand hand)
    {
        return hand != null && hand.IsTracked && hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
    }

    private void FollowHand(OVRHand hand)
    {
        Vector3 rail = _railEnd.position - _railStart.position;
        float length = rail.magnitude;
        if (length < 0.0001f) return;

        Vector3 railDir = rail / length;
        Vector3 toHand = hand.PointerPose.position - _railStart.position;
        float t = Mathf.Clamp01(Vector3.Dot(toHand, railDir) / length);
        SetNormalized(t);
    }

    private void SetNormalized(float t)
    {
        _handle.position = Vector3.Lerp(_railStart.position, _railEnd.position, t);
        Value = Mathf.RoundToInt(Mathf.Lerp(_minValue, _maxValue, t));
        if (_valueText != null) _valueText.text = Value.ToString();
    }
}
