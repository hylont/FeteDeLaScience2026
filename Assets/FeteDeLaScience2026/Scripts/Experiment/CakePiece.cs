using UnityEngine;
using UnityEngine.InputSystem;

// The virtual cake piece. It does not use physics-based hand grabbing: since it has to stay
// glued to a real piece of cake the participant is actually holding, the experimenter manually
// confirms the grab by holding Ctrl, and the piece snaps to whichever tracked hand is closest
// to it at that moment for as long as Ctrl stays held.
public class CakePiece : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Renderer _renderer;

    public bool IsBeingHeld { get; private set; }

    private MaterialPropertyBlock _propertyBlock;
    private OVRHand _followingHand;
    private bool _wasAttachHeld;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        bool attachHeld = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);

        if (attachHeld && !_wasAttachHeld)
        {
            _followingHand = FindClosestTrackedHand();
            if (_followingHand != null)
            {
                IsBeingHeld = true;
            }
        }

        if (attachHeld && _followingHand != null && _followingHand.IsTracked)
        {
            transform.SetPositionAndRotation(_followingHand.PointerPose.position, _followingHand.PointerPose.rotation);
        }

        _wasAttachHeld = attachHeld;
    }

    public void SetColor(Color color)
    {
        if (_renderer == null)
        {
            LLogger.W("CakePiece has no Renderer assigned; cannot apply flavor color.");
            return;
        }

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    public void ResetToServingPoint(Transform servingPoint)
    {
        IsBeingHeld = false;
        _followingHand = null;
        _wasAttachHeld = false;

        if (servingPoint != null)
        {
            transform.SetPositionAndRotation(servingPoint.position, servingPoint.rotation);
        }
    }

    private OVRHand FindClosestTrackedHand()
    {
        OVRHand left = ExperimentHands.Instance.Left;
        OVRHand right = ExperimentHands.Instance.Right;

        OVRHand best = null;
        float bestDist = float.MaxValue;

        if (left != null && left.IsTracked)
        {
            float d = Vector3.Distance(left.PointerPose.position, transform.position);
            if (d < bestDist) { bestDist = d; best = left; }
        }

        if (right != null && right.IsTracked)
        {
            float d = Vector3.Distance(right.PointerPose.position, transform.position);
            if (d < bestDist) { bestDist = d; best = right; }
        }

        return best;
    }
}
