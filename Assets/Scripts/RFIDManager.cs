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
    public string portName = "COM12";
    public int baudRate = 9600;

    [Header("Referencias UI")]
    public Image imagenUI;
    public AudioSource audioSource;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;

    [Header("Cola de reproduccion")]
    public float cooldownRelectura = 1.5f;

    SerialPort serialPort;
    bool modoTeclado = false;
    bool reproduciendo = false;
    Queue<RFIDEntry> colaEntradas = new Queue<RFIDEntry>();
    Dictionary<string, float> ultimaLectura = new Dictionary<string, float>();

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
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        if (ultimaLectura.TryGetValue(id, out float tiempoAnterior) && Time.time - tiempoAnterior < cooldownRelectura)
        {
            return;
        }

        RFIDEntry entrada = database.BuscarPorId(id);

        if (entrada == null)
        {
            Debug.LogWarning("ID no registrada: " + id);
            return;
        }

        ultimaLectura[id] = Time.time;
        colaEntradas.Enqueue(entrada);

        if (!reproduciendo)
        {
            StartCoroutine(ProcesarCola());
        }
    }

    IEnumerator ProcesarCola()
    {
        reproduciendo = true;

        while (colaEntradas.Count > 0)
        {
            RFIDEntry entrada = colaEntradas.Dequeue();
            yield return ReproducirEntrada(entrada);
        }

        reproduciendo = false;
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