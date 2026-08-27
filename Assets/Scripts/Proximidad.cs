using UnityEngine;
using System.IO.Ports;

public class Proximidad : MonoBehaviour
{
    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 9600;

    [Header("Configuracion")]
    public float distanciaUmbralCm = 100f;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;
    public KeyCode teclaDebug = KeyCode.P;

    SerialPort serialPort;
    bool modoTeclado = false;
    bool yaReproducido = false;

    void Start()
    {
        if (forzarModoTeclado)
        {
            modoTeclado = true;
            Debug.Log("Modo teclado forzado, no se intenta abrir el puerto serial.");
            return;
        }

        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            serialPort.ReadTimeout = 25;
            Debug.Log("Puerto abierto exitosamente.");
        }
        catch (System.Exception e)
        {
            modoTeclado = true;
            Debug.LogWarning("No se pudo abrir el puerto, usando modo teclado: " + e.Message);
        }
    }

    void Update()
    {
        if (yaReproducido)
        {
            return;
        }

        if (modoTeclado)
        {
            if (Input.GetKeyDown(teclaDebug))
            {
                Reproducir();
            }
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string dato = serialPort.ReadLine().Trim();

                if (float.TryParse(dato, out float distanciaCm))
                {
                    if (distanciaCm <= distanciaUmbralCm)
                    {
                        Reproducir();
                    }
                }
            }
            catch (System.Exception) { }
        }
    }

    void Reproducir()
    {
        yaReproducido = true;

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}
