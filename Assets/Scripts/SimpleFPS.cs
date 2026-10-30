using UnityEngine;

public class SimpleFPS : MonoBehaviour
{
    public float vitesse = 5f;
    public float sensibilite = 2f;
    public Transform cam;

    CharacterController controller;
    float rotationX = 0f;
    float vitesseY = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float sourisX = Input.GetAxis("Mouse X") * sensibilite;
        float sourisY = Input.GetAxis("Mouse Y") * sensibilite;

        transform.Rotate(0f, sourisX, 0f);
        rotationX = Mathf.Clamp(rotationX - sourisY, -80f, 80f);
        cam.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 mouvement = transform.right * h + transform.forward * v;

        if (controller.isGrounded) vitesseY = -1f;
        else vitesseY += Physics.gravity.y * Time.deltaTime;

        mouvement = mouvement * vitesse + Vector3.up * vitesseY;
        controller.Move(mouvement * Time.deltaTime);
    }
}