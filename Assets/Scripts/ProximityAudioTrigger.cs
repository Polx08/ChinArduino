using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Globalization;

public class ProximityAudioTrigger : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Bloqueo inicial (opcional)")]
    [Tooltip("Si se asigna, el sensor no activa nada hasta que el telefono se conecte")]
    public PhoneConnectionServer conexionTelefono;

    [Header("Configuracion")]
    public float distanciaUmbralCm = 100f;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Anillo LED WS2812")]
    public int cantidadLedsAnillo = 16;
    public Color colorLeds = Color.white;

    [Header("UI Distancia")]
    public TMP_Text textoDistancia;
    public float tiempoOcultarSinLectura = 0.5f;

    [Header("Debug sin Arduino")]
    public float distanciaSimuladaDebug = 150f;
    public KeyCode teclaDebug = KeyCode.P;

    public event System.Action OnActivado;

    bool yaReproducido = false;
    bool telefonoConectado = false;
    float ultimaLecturaTiempo = -999f;

    void Start()
    {
        if (textoDistancia != null)
        {
            textoDistancia.gameObject.SetActive(false);
        }

        if (conexionTelefono != null)
        {
            telefonoConectado = conexionTelefono.TelefonoConectado;
            conexionTelefono.OnTelefonoConectado += AlConectarTelefono;
        }
        else
        {
            telefonoConectado = true;
        }

        if (serial != null)
        {
            serial.OnLineaRecibida += ProcesarLinea;
        }
    }

    void AlConectarTelefono()
    {
        telefonoConectado = true;
    }

    void ProcesarLinea(string dato)
    {
        if (float.TryParse(dato, NumberStyles.Float, CultureInfo.InvariantCulture, out float distanciaCm))
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

        if (!yaReproducido && telefonoConectado && distanciaCm >= distanciaUmbralCm)
        {
            Reproducir();
        }
    }

    public void ActivarManual()
    {
        if (!yaReproducido)
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

        if (conexionTelefono != null)
        {
            conexionTelefono.OnTelefonoConectado -= AlConectarTelefono;
        }
    }
}