using Assets;
using UnityEngine;
using Object = UnityEngine.Object;

public class SpawnObjects : MonoBehaviour 
{
    // Use this for initialization
    void Start () 
    {
        Application.targetFrameRate = -1;
        
        parent1 = new GameObject();

        for (int i = 0; i < 15000; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Object.Destroy(sphere.GetComponent<Collider>());

            sphere.AddComponent<DoSomethingHeavy>();

            sphere.transform.parent = parent1.transform;
        }

        parent2 = new GameObject();
        
        for (int i = 0; i < 15000; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Object.Destroy(sphere.GetComponent<Collider>());

            sphere.AddComponent<DoSomethingHeavy2>();

            sphere.transform.parent = parent2.transform;
        }

        parent2.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.anyKeyDown)
        {
            parent1.SetActive(!parent1.activeSelf);
            parent2.SetActive(!parent2.activeSelf);
        }
    }

    GameObject parent1;
    GameObject parent2;
}
