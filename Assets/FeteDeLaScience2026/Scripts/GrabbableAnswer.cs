using Oculus.Interaction;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// An answer sphere: it only counts once held for _holdDuration, to filter out accidental grabs.
// Released earlier, it glides back to its place.
[RequireComponent(typeof(Grabbable))]
public class GrabbableAnswer : MonoBehaviour
{
    [Tooltip("Raised once the answer has been held for the hold duration.")]
    [SerializeField] UnityEvent _onGrabbed;
    [SerializeField] ParticleSystem _grabbedEffect;

    [Header("Validation")]
    [Tooltip("Seconds the answer must be held before it counts.")]
    [SerializeField] float _holdDuration = 2f;
    [Tooltip("How fast a released answer goes back to its place.")]
    [SerializeField] float _returnSpeed = 5f;

    [Header("Visual")]
    [SerializeField, Range(1f, 2f)] float _hoverScale = 1.2f;
    [SerializeField] float _scaleSpeed = 5f;
    Vector3 _baseScale;
    Vector3 _basePosition;
    Quaternion _baseRotation;
    Grabbable _grabbable;
    float _heldTime;
    bool _validated;

    // Number of interactors hovering the sphere, so it stays hovered while one hand leaves and the other remains.
    bool IsHovered => _grabbable.PointsCount > 0;
    bool IsHeld => _grabbable.SelectingPointsCount > 0;

    // Raised once the answer has been held for the hold duration.
    public event Action<GrabbableAnswer> Grabbed;

    // The answer's title, as displayed above the sphere.
    public string Label => GetComponentInChildren<TMP_Text>(true).text;

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();

        _baseScale = transform.localScale;
        transform.GetLocalPositionAndRotation(out _basePosition, out _baseRotation);
    }

    // An answer can be shown several times, but grabbing it moves it: put it back each time it reappears.
    void OnEnable()
    {
        _heldTime = 0f;
        _validated = false;
        transform.localScale = _baseScale;
        transform.SetLocalPositionAndRotation(_basePosition, _baseRotation);
    }

    void Update()
    {
        UpdateHold();
        UpdateHoverScale();
    }

    void UpdateHold()
    {
        if (IsHeld)
        {
            _heldTime += Time.deltaTime;
            if (!_validated && _heldTime >= _holdDuration) Validate();
            return;
        }

        _heldTime = 0f;
        _validated = false;

        float t = Time.deltaTime * _returnSpeed;
        transform.SetLocalPositionAndRotation(
            Vector3.Lerp(transform.localPosition, _basePosition, t),
            Quaternion.Slerp(transform.localRotation, _baseRotation, t));
    }

    void Validate()
    {
        _validated = true;
        _onGrabbed?.Invoke();
        Grabbed?.Invoke(this);
        if(_grabbedEffect) Instantiate(_grabbedEffect, transform.position, transform.rotation);
    }

    void UpdateHoverScale()
    {
        if (IsHovered && transform.localScale.x < _baseScale.x * _hoverScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * _hoverScale, Time.deltaTime * _scaleSpeed);
        }
        else if (!IsHovered && transform.localScale.x > _baseScale.x)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * _scaleSpeed);
        }
    }
}
