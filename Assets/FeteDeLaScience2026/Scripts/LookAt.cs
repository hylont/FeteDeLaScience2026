using UnityEngine;

public class LookAt : MonoBehaviour
{
    [SerializeField] float _maxDistance = 2f;
    [Header("Targets")]
    public Transform Target = null;
    [SerializeField] bool m_getCameraIfNoTarget = true;

    [Header("Rotation restrictions")]
    [SerializeField] bool m_lockX = false;
    [SerializeField] bool m_lockY = false;
    [SerializeField] bool m_lockZ = false;

    [Header("Smoothing")]
    [SerializeField] float m_speed = 1.0f;

    Quaternion _defaultRotation;

    private void Start()
    {
        _defaultRotation = transform.rotation;

        if (Target == null && m_getCameraIfNoTarget)
        {
            SetCameraAsTarget();
        }
    }

    private void Update()
    {
        if (Target == null) return;

        if(Vector3.Distance(transform.position, Target.position) > _maxDistance)
        {
            transform.rotation = _defaultRotation;
            return;
        }

        Vector3 l_previousEuler = transform.eulerAngles;

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(Target.position - transform.position), Time.deltaTime * m_speed);

        transform.rotation = Quaternion.Euler(
            m_lockX ? l_previousEuler.x : transform.eulerAngles.x,
            m_lockY ? l_previousEuler.y : transform.eulerAngles.y,
            m_lockZ ? l_previousEuler.z : transform.eulerAngles.z);

    }

    public void SetCameraAsTarget()
    {
        GameObject m_targetCamera = GameObject.FindWithTag("MainCamera");

        if (m_targetCamera != null) Target = m_targetCamera.transform;
        else
        {
            Debug.LogError("[LOOKAT] No camera target found ! Please affect a target in the Editor");
        }
    }
}