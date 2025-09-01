using UnityEngine;

public class Home : MonoBehaviour
{
    public Transform nest;
    public PheromoneField homeField;
    public float homeSourceRate = 10f;
    void Update() => homeField.Deposit(nest.position, homeSourceRate * Time.deltaTime);
}
