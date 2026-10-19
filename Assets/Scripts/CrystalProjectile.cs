using UnityEngine;

public class CrystalProjectile : MonoBehaviour
{
    public float speed = 40.0f;
    public float lifeTime = 2.0f; 

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- LA SOLUCIÓN ---
        // Si el objeto contra el que chocamos tiene el script TargetBox...
        if (other.CompareTag(SpatialStimulusToken.StimulusTag) || other.CompareTag("Pickup"))
        {
            return;
        }

        if (other.GetComponent<TargetBox>() != null)
        {
            // Abortamos este código. Dejamos que TargetBox decida si explota o no.
            return; 
        }

        // Si NO es una caja, pero SÍ es un asteroide normal, lo destruimos como siempre:
        if (other.CompareTag("Obstacle"))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }
}