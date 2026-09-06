using UnityEngine;

public class MotorController : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Velocidad")]
    [Tooltip("Milisegundos entre cada paso del motor. Mas alto = mas lento.")]
    public int velocidadMs = 2;

    [Header("Debug sin Arduino")]
    public KeyCode teclaDebugGirar = KeyCode.M;
    public float duracionDebugSegundos = 2f;

    int ultimaVelocidadEnviada = -1;

    void Start()
    {
        if (serial != null)
        {
            serial.OnConectado += EnviarVelocidad;
        }
    }

    void Update()
    {
        if (velocidadMs != ultimaVelocidadEnviada)
        {
            EnviarVelocidad();
        }

        if (Input.GetKeyDown(teclaDebugGirar))
        {
            GirarPorTiempo(duracionDebugSegundos);
        }
    }

    public void EnviarVelocidad()
    {
        if (serial == null)
        {
            return;
        }

        ultimaVelocidadEnviada = velocidadMs;
        serial.EnviarComando("MOTOR:VEL:" + velocidadMs);
    }

    public void GirarPorTiempo(float segundos)
    {
        if (serial == null)
        {
            return;
        }

        int ms = Mathf.RoundToInt(segundos * 1000f);
        serial.EnviarComando("MOTOR:TIEMPO:" + ms);
    }

    public void GirarVueltaCompleta()
    {
        if (serial != null)
        {
            serial.EnviarComando("MOTOR:ON");
        }
    }

    void OnDisable()
    {
        if (serial != null)
        {
            serial.OnConectado -= EnviarVelocidad;
        }
    }
}
