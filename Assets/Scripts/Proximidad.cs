using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Proximidad : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Configuracion")]
    public float distanciaUmbralCm = 100f;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Anillo LED WS2812")]
    public int cantidadLedsAnillo = 16;
    public Color colorLeds = Color.white;

    [Header("UI Distancia")]
    public Text textoDistancia;
    public float tiempoOcultarSinLectura = 0.5f;

    [Header("Debug sin Arduino")]
    public float distanciaSimuladaDebug = 150f;
    public KeyCode teclaDebug = KeyCode.P;

    public event System.Action OnActivado;

    bool yaReproducido = false;
    float ultimaLecturaTiempo = -999f;

    void Start()
    {
        if (textoDistancia != null)
        {
            textoDistancia.gameObject.SetActive(false);
        }

        if (serial != null)
        {
            serial.OnLineaRecibida += ProcesarLinea;
        }
    }

    void ProcesarLinea(string dato)
    {
        if (float.TryParse(dato, out float distanciaCm))
        {
            RegistrarLectura(distanciaCm);
        }
    }

    void Update()
    {
        if (serial != null && serial.ModoTeclado && Input.GetKey(teclaDebug))
        {
            RegistrarLectura(distanciaSimuladaDebug);
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

        if (!yaReproducido && distanciaCm >= distanciaUmbralCm)
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

        if (serial != null)
        {
            List<int> todosLosLeds = new List<int>();
            for (int i = 0; i < cantidadLedsAnillo; i++)
            {
                todosLosLeds.Add(i);
            }

            string indicesStr = string.Join(",", todosLosLeds);
            string colorHex = ColorUtility.ToHtmlStringRGB(colorLeds);
            serial.EnviarComando("LED:" + indicesStr + ":" + colorHex);
        }

        OnActivado?.Invoke();
    }

    void OnDisable()
    {
        if (serial != null)
        {
            serial.OnLineaRecibida -= ProcesarLinea;
        }
    }
}