using UnityEngine;

public class Rotation : MonoBehaviour
{
  public float rotationSpeed = 1;
  float angle = 0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
      angle = angle + rotationSpeed;
      transform.localEulerAngles = new Vector3(angle, 0, angle);
    }
}
