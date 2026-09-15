using UnityEngine;
using UnityEngine.Events;

// Generic hand-tracking "button": fires once when a fingertip enters its poke radius,
// and re-arms only after the finger has left again. Used instead of Meta's poke building
// blocks so the UI works without any extra Editor wiring beyond placing this component.
public class HandPokeButton : MonoBehaviour
{
    [SerializeField] private float _pokeRadius = 0.03f;

    public UnityEvent OnPoked;

    private bool _wasTouching;

    private void Update()
    {
        bool touching = IsAnyFingertipInRange();

        if (touching && !_wasTouching)
        {
            OnPoked?.Invoke();
        }

        _wasTouching = touching;
    }

    private bool IsAnyFingertipInRange()
    {
        OVRHand left = ExperimentHands.Instance.Left;
        OVRHand right = ExperimentHands.Instance.Right;

        if (left != null && left.IsTracked &&
            Vector3.Distance(left.PointerPose.position, transform.position) <= _pokeRadius)
        {
            return true;
        }

        if (right != null && right.IsTracked &&
            Vector3.Distance(right.PointerPose.position, transform.position) <= _pokeRadius)
        {
            return true;
        }

        return false;
    }
}
