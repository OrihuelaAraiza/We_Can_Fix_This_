using UnityEngine;

public class DropZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        BoxItem box = other.GetComponent<BoxItem>();

        if (box != null)
        {

            box.delivered = true;
            Destroy(box.gameObject);
        }
    }
}
