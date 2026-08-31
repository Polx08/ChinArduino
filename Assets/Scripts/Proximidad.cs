using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using System.Collections.Generic;

public class Proximidad : MonoBehaviour
{
    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 9600;

    [Header("Configuracion")]
    public float distanciaUmbralCm = 100f;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Anillo LED WS2812 (16 leds)")]
    public List<int> ledsIndices = new List<int>();
    public Color colorLeds = Color.white;

    [Header("UI Distancia")]
    public Text textoDistancia;
    public float tiempoOcultarSinLectura = 0.5f;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;
    public KeyCode teclaDebug = KeyCode.P;
    public float distanciaSimuladaDebug = 50f;

    SerialPort serialPort;
    bool modoTeclado = false;
    bool yaReproducido = false;
    float ultimaLecturaTiempo = -999f;

    void Start()
    {
        if (textoDistancia != null)
        {
            textoDistancia.gameObject.SetActive(false);
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
        if (modoTeclado)
        {
            if (Input.GetKey(teclaDebug))
            {
                RegistrarLectura(distanciaSimuladaDebug);
            }
        }
        else if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string dato = serialPort.ReadLine().Trim();

                if (float.TryParse(dato, out float distanciaCm))
                {
                    RegistrarLectura(distanciaCm);
                }
            }
            catch (System.Exception) { }
        }

        ActualizarVisibilidadTexto();
    }

    void RegistrarLectura(float distanciaCm)
    {
        ultimaLecturaTiempo = Time.time;

        if (textoDistancia != null)
        {
            textoDistancia.gameObject.SetActive(true);
            textoDistancia.text = distanciaCm.ToString("0") + " cm";
        }

        if (!yaReproducido && distanciaCm <= distanciaUmbralCm)
        {
            Reproducir();
        }
    }

    void ActualizarVisibilidadTexto()
    {
        if (textoDistancia == null)
        {
            return;
        }

        if (Time.time - ultimaLecturaTiempo > tiempoOcultarSinLectura)
        {
            textoDistancia.gameObject.SetActive(false);
        }
    }

    void Reproducir()
    {
        yaReproducido = true;

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }

        if (ledsIndices.Count > 0)
        {
            EnviarComandoLeds(ledsIndices, colorLeds);
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