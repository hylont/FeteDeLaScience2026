using UnityEngine;

// The virtual cake piece. It does not use physics-based hand grabbing: since it has to stay
// glued to a real piece of cake the participant is actually holding, the experimenter manually
// confirms the grab by holding the left controller's grip. While held (and until the piece has
// reached the mouth), the piece is pinned to the participant's RIGHT hand pinch pose — always
// the right hand, never whichever hand happens to be closest.
public class CakePiece : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Renderer _renderer;

    public bool IsBeingHeld { get; private set; }

    private MaterialPropertyBlock _propertyBlock;
    private bool _locked;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_locked) return;

        bool attachHeld = OVRInput.Get(OVRInput.RawButton.LHandTrigger, OVRInput.Controller.LTouch);
        if (!attachHeld) return;

        OVRHand rightHand = ExperimentHands.Instance.Right;
        if (rightHand == null || !rightHand.IsTracked) return;

        transform.SetPositionAndRotation(rightHand.PointerPose.position, rightHand.PointerPose.rotation);
        IsBeingHeld = true;
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
        _locked = false;

        if (servingPoint != null)
        {
            transform.SetPositionAndRotation(servingPoint.position, servingPoint.rotation);
        }
    }

    // Called once the piece has reached the mouth: it stops tracking the hand and stays put.
    public void StopFollowing()
    {
        _locked = true;
    }
}
