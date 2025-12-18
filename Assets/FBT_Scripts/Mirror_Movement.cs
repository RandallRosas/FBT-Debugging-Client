using UnityEngine;

// Couldn't get this script working how I wanted to sadly.
public class Mirror_Movement : MonoBehaviour
{
    public Transform mirror;
    public Transform playerTarget;

    void Start()
    {

    }

    void Update()
    {
        Vector3 localPlayer = mirror.InverseTransformPoint(playerTarget.position);
        transform.position = mirror.TransformPoint(new Vector3(localPlayer.x, localPlayer.y, localPlayer.z));

        Vector3 lookatmirror = mirror.TransformPoint(new Vector3(-localPlayer.x, localPlayer.y, localPlayer.z));
        transform.LookAt(lookatmirror);
    }
}
