using UnityEngine;
using System.IO.Ports;
using System;

public class ArduinoSerialManager : MonoBehaviour
{
    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 9600;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;

    public bool ModoTeclado { get; private set; }

    public event Action<string> OnLineaRecibida;
    public event Action OnConectado;

    SerialPort serialPort;

    void Start()
    {
        if (forzarModoTeclado)
        {
            ModoTeclado = true;
            Debug.Log("Modo teclado forzado, no se intenta abrir el puerto serial.");
            return;
        }

        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            serialPort.ReadTimeout = 25;
            Debug.Log("Puerto abierto exitosamente.");
            OnConectado?.Invoke();
        }
        catch (Exception e)
        {
            ModoTeclado = true;
            Debug.LogWarning("No se pudo abrir el puerto, usando modo teclado: " + e.Message);
        }
    }

    void Update()
    {
        if (ModoTeclado || serialPort == null || !serialPort.IsOpen)
        {
            return;
        }

        try
        {
            string dato = serialPort.ReadLine().Trim();
            OnLineaRecibida?.Invoke(dato);
        }
        catch (Exception) { }
    }

    public void EnviarComando(string comando)
    {
        if (ModoTeclado)
        {
            Debug.Log("Modo teclado, comando no enviado: " + comando);
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.WriteLine(comando);
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo enviar comando: " + e.Message);
            }
        }
    }

    void OnDisable()
    {
        CerrarPuerto();
    }

    void OnApplicationQuit()
    {
        CerrarPuerto();
    }

    void CerrarPuerto()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}
