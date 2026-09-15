using UnityEngine;

// Adjusts the XR rig's height and yaw so the real table lines up with the virtual one.
// Sampled once, when the experimenter confirms both hands are resting flat on the table.
public class CalibrationRig : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private Transform _xrOrigin;
    [SerializeField] private Transform _headTransform;

    [Header("Target")]
    [Tooltip("World-space height (Y) of the virtual table surface.")]
    [SerializeField] private float _targetTableHeight = 0.75f;

    [Tooltip("Direction the participant should be facing once calibrated (world space).")]
    [SerializeField] private Vector3 _targetForward = Vector3.forward;

    public bool TryCalibrate()
    {
        OVRHand left = ExperimentHands.Instance.Left;
        OVRHand right = ExperimentHands.Instance.Right;

        if (left == null || right == null || !left.IsTracked || !right.IsTracked)
        {
            LLogger.W("Calibration aborted: both hands must be tracked.");
            return false;
        }

        Vector3 leftPos = left.PointerPose.position;
        Vector3 rightPos = right.PointerPose.position;

        Vector3 handAxis = rightPos - leftPos;
        handAxis.y = 0f;
        if (handAxis.sqrMagnitude < 0.0001f)
        {
            LLogger.W("Calibration aborted: hands are too close together to read an orientation.");
            return false;
        }
        handAxis.Normalize();

        float handHeight = (leftPos.y + rightPos.y) * 0.5f;
        float deltaHeight = _targetTableHeight - handHeight;
        _xrOrigin.position += Vector3.up * deltaHeight;

        Vector3 targetRight = Vector3.Cross(Vector3.up, _targetForward.normalized);
        float deltaYaw = Vector3.SignedAngle(handAxis, targetRight, Vector3.up);
        _xrOrigin.RotateAround(_headTransform.position, Vector3.up, deltaYaw);

        LLogger.L($"Calibration applied — deltaHeight={deltaHeight:F3}m, deltaYaw={deltaYaw:F1} deg");
        return true;
    }
}
