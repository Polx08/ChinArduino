using UnityEngine;
using System.IO.Ports;

public class ArduinoReader : MonoBehaviour
{
    private string portName = "COM10";
    private int baudRate = 9600;

    private SerialPort serialPort;
    private float sensorValue = 0f;

    public SpriteRenderer spriteAEditar;
    public Color colorNormal = Color.white;
    public Color colorPresionado = Color.green;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;
    public KeyCode teclaDebug = KeyCode.Space;
    public float valorTecladoPresionado = 1f;
    public float valorTecladoSuelto = 0f;

    bool modoTeclado = false;

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
            serialPort.ReadTimeout = 1;
            Debug.Log("Serial Port Abierto en: " + portName);
        }
        catch (System.Exception e)
        {
            modoTeclado = true;
            Debug.LogWarning("No se pudo abrir el puerto, usando modo teclado: " + e.Message);
        }
    }

    void Update()
    {
        if (modoTeclado)
        {
            bool presionado = Input.GetKey(teclaDebug);
            sensorValue = presionado ? valorTecladoPresionado : valorTecladoSuelto;
            spriteAEditar.color = presionado ? colorPresionado : colorNormal;
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string data = serialPort.ReadLine();
                if (data.Contains("1"))
                {
                    spriteAEditar.color = colorPresionado;
                }
                else if (data.Contains("0"))
                {
                    spriteAEditar.color = colorNormal;
                }

                sensorValue = float.Parse(data);
                Debug.Log("Dato de Arduino: " + sensorValue);
            }
            catch (System.TimeoutException)
            {
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error en lectura serial: " + e.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("Serial Port Cerrado.");
        }
    }
}