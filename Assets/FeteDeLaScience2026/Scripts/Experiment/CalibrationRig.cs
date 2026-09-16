using EditorAttributes;
using System.Collections;
using UnityEngine;

// Adjusts the XR rig's height and yaw so the real table lines up with the virtual one.
// Sampled once, when the experimenter confirms both hands are resting flat on the table.
public class CalibrationRig : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private Transform _headTransform;

    [Header("Target")]
    [Tooltip("World-space height (Y) of the virtual table surface.")]
    [SerializeField] private float _baseTableHeight = 1f;

    [Header("Fader")]
    [SerializeField] private ScreenFader _fader;

    IEnumerator DelayCalibration()
    {
        yield return new WaitForSeconds(_fader.FadeDuration);
        ForceCalibrate();
        _fader.FadeToHidden();
    }

    public void TryCalibrate()
    {
        if(_fader != null)
        {
            _fader.FadeToBlack();
            StartCoroutine(DelayCalibration());
        }
        else
        {
            ForceCalibrate();
        }
    }

    [Button("Force Calibrate")]
    void ForceCalibrate()
    {
        OVRHand left = ExperimentHands.Instance.Left;
        OVRHand right = ExperimentHands.Instance.Right;

        if (left == null || right == null || !left.IsTracked || !right.IsTracked)
        {
            LLogger.W("Calibration aborted: both hands must be tracked.");
            return;
        }

        Vector3 RHandPos = right.PointerPose.position;
        Vector3 LHandPos = left.PointerPose.position;
        transform.position = new Vector3((RHandPos.x + LHandPos.x) * 0.5f,
            -_baseTableHeight + (RHandPos.y + LHandPos.y) * 0.5f,
            (RHandPos.z + LHandPos.z) * 0.5f);

        Quaternion oldRot = transform.rotation;
        transform.LookAt(_headTransform.position);
        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

        LLogger.W($"Calibration complete. New height = - {_baseTableHeight} + ({RHandPos.y} + {LHandPos.y}) * 0.5f) = {transform.position.y}\n New rotation: {transform.rotation.eulerAngles.ToString()}");
    }
}
