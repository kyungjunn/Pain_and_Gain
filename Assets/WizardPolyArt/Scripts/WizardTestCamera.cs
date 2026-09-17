using UnityEngine;

public sealed class WizardTestCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5.5f, -8f);
    [SerializeField] private float followSpeed = 8f;

    public void SetTarget(Transform value)
    {
        target = value;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}
