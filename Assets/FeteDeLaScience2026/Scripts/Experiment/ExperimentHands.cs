using UnityEngine;

// Single place to wire up the two tracked hands (from the Meta hand-tracking rig)
// so every other experiment script can read them without its own Inspector slot.
public class ExperimentHands : MonoBehaviour
{
    public static ExperimentHands Instance { get; private set; }

    [SerializeField] private OVRHand _left;
    [SerializeField] private OVRHand _right;

    public OVRHand Left => _left;
    public OVRHand Right => _right;
    public Transform RightIndexTip, RightThumbTip;

    private void Awake()
    {
        Instance = this;
    }
}
