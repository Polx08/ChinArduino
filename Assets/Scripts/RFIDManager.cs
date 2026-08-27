using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using System.Collections;
using System.Collections.Generic;

public class RFIDManager : MonoBehaviour
{
    [Header("Base de datos")]
    public RFIDDatabase database;

    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 9600;

    [Header("Referencias UI")]
    public Image imagenUI;
    public AudioSource audioSource;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;

    SerialPort serialPort;
    bool modoTeclado = false;
    HashSet<string> idsReproducidas = new HashSet<string>();

    void Start()
    {
        if (imagenUI != null)
        {
            imagenUI.enabled = false;
        }

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
        if (database == null)
        {
            return;
        }

        if (modoTeclado)
        {
            foreach (var entrada in database.entradas)
            {
                if (entrada.teclaDebug != KeyCode.None && Input.GetKeyDown(entrada.teclaDebug))
                {
                    ProcesarId(entrada.id);
                }
            }
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string dato = serialPort.ReadLine();
                ProcesarId(dato.Trim());
            }
            catch (System.Exception) { }
        }
    }

    void ProcesarId(string id)
    {
        if (string.IsNullOrEmpty(id) || idsReproducidas.Contains(id))
        {
            return;
        }

        RFIDEntry entrada = database.BuscarPorId(id);

        if (entrada == null)
        {
            Debug.LogWarning("ID no registrada: " + id);
            return;
        }

        idsReproducidas.Add(id);
        StartCoroutine(ReproducirEntrada(entrada));
    }

    IEnumerator ReproducirEntrada(RFIDEntry entrada)
    {
        if (imagenUI != null && entrada.imagen != null)
        {
            imagenUI.sprite = entrada.imagen;
            imagenUI.enabled = true;
        }

        if (entrada.ledsIndices.Count > 0)
        {
            EnviarComandoLeds(entrada.ledsIndices, entrada.colorLeds);
        }

        float duracion = entrada.duracionImagen > 0f ? entrada.duracionImagen : 0f;

        if (audioSource != null && entrada.audio != null)
        {
            audioSource.clip = entrada.audio;
            audioSource.Play();

            if (entrada.duracionImagen <= 0f)
            {
                duracion = entrada.audio.length;
            }
        }

        yield return new WaitForSeconds(duracion);

        if (imagenUI != null)
        {
            imagenUI.enabled = false;
        }
    }

    void EnviarComandoLeds(List<int> indices, Color color)
    {
        string indicesStr = string.Join(",", indices);
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        string comando = "LED:" + indicesStr + ":" + colorHex;

        if (modoTeclado)
        {
            Debug.Log("Modo teclado, comando LED no enviado: " + comando);
            return;
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.WriteLine(comando);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("No se pudo enviar comando LED: " + e.Message);
            }
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