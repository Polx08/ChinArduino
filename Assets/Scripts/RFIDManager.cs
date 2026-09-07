using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class RFIDManager : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Base de datos")]
    public RFIDDatabase database;

    [Header("Referencias UI")]
    public Image imagenUI;
    public AudioSource audioSource;
    public AudioSource musicaSource;

    [Header("Bloqueo inicial (opcional)")]
    [Tooltip("Si se asigna, el RFID no responde hasta que este sensor se active (ej: abrir el cofre)")]
    public Proximidad sensorInicio;

    [Header("Cola de reproduccion")]
    [Tooltip("Tiempo minimo entre lecturas de la misma id, para evitar que una sola pasada se registre varias veces")]
    public float cooldownRelectura = 1.5f;

    bool reproduciendo = false;
    bool ledsApagados = false;
    bool cofreAbierto = false;
    TagPair parEnEspera = null;
    HashSet<TagPair> paresCompletados = new HashSet<TagPair>();
    Queue<IEnumerator> cola = new Queue<IEnumerator>();
    Dictionary<string, float> ultimaLectura = new Dictionary<string, float>();

    void Start()
    {
        if (imagenUI != null)
        {
            imagenUI.enabled = false;
        }

        if (sensorInicio != null)
        {
            sensorInicio.OnActivado += () => cofreAbierto = true;
        }
        else
        {
            cofreAbierto = true;
        }

        if (serial != null)
        {
            serial.OnLineaRecibida += ProcesarId;
            serial.OnConectado += ApagarLedsAlConectar;
        }
    }

    void ApagarLedsAlConectar()
    {
        if (!ledsApagados)
        {
            ledsApagados = true;
            serial.EnviarComando("LED:OFF");
        }
    }

    void Update()
    {
        if (database == null || serial == null || !serial.ModoTeclado || !cofreAbierto)
        {
            return;
        }

        foreach (var par in database.pares)
        {
            if (par.principal != null && par.principal.teclaDebug != KeyCode.None && Input.GetKeyDown(par.principal.teclaDebug))
            {
                ProcesarId(par.principal.id);
            }

            if (par.pareja != null && par.pareja.teclaDebug != KeyCode.None && Input.GetKeyDown(par.pareja.teclaDebug))
            {
                ProcesarId(par.pareja.id);
            }
        }
    }

    void ProcesarId(string id)
    {
        if (database == null || string.IsNullOrEmpty(id))
        {
            return;
        }

        if (!Regex.IsMatch(id, @"^[0-9A-Fa-f]+$"))
        {
            return;
        }

        Debug.Log("RFID leido: " + id);

        if (!cofreAbierto)
        {
            return;
        }

        if (ultimaLectura.TryGetValue(id, out float tiempoAnterior) && Time.time - tiempoAnterior < cooldownRelectura)
        {
            return;
        }

        RFIDEntry entrada = database.BuscarEntradaPorId(id);

        if (entrada == null)
        {
            return;
        }

        ultimaLectura[id] = Time.time;
        cola.Enqueue(ReproducirEntrada(entrada));

        ActualizarProgreso(id);
        LogProgreso();

        if (!reproduciendo)
        {
            StartCoroutine(ProcesarCola());
        }
    }

    void ActualizarProgreso(string id)
    {
        if (parEnEspera != null)
        {
            bool esLaPareja = parEnEspera.pareja != null &&
                parEnEspera.pareja.id.Trim().Equals(id.Trim(), System.StringComparison.OrdinalIgnoreCase);

            if (esLaPareja)
            {
                paresCompletados.Add(parEnEspera);
                parEnEspera = null;

                if (paresCompletados.Count >= database.pares.Count)
                {
                    cola.Enqueue(FinalizarExperiencia());
                }
            }

            return;
        }

        TagPair par = BuscarParPorPrincipalPendiente(id);

        if (par != null)
        {
            parEnEspera = par;
        }
    }

    void LogProgreso()
    {
        List<string> pendientes = new List<string>();

        foreach (var par in database.pares)
        {
            if (paresCompletados.Contains(par))
            {
                continue;
            }

            if (par == parEnEspera)
            {
                string idPareja = par.pareja != null ? par.pareja.id : "(sin id de pareja asignada)";
                pendientes.Add("esperando pareja de '" + idPareja + "'");
            }
            else
            {
                string idPrincipal = par.principal != null ? par.principal.id : "(sin id principal asignada)";
                pendientes.Add(idPrincipal);
            }
        }

        if (pendientes.Count == 0)
        {
            Debug.Log("Todos los IDs fueron escaneados.");
        }
        else
        {
            Debug.Log("IDs restantes por escanear (" + pendientes.Count + "): " + string.Join(", ", pendientes));
        }
    }

    TagPair BuscarParPorPrincipalPendiente(string id)
    {
        foreach (var par in database.pares)
        {
            if (paresCompletados.Contains(par))
            {
                continue;
            }

            if (par.principal != null && par.principal.id.Trim().Equals(id.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return par;
            }
        }
        return null;
    }

    IEnumerator ProcesarCola()
    {
        reproduciendo = true;

        while (cola.Count > 0)
        {
            yield return cola.Dequeue();
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

        if (entrada.ledsIndices.Count > 0 && serial != null)
        {
            string indicesStr = string.Join(",", entrada.ledsIndices);
            string colorHex = ColorUtility.ToHtmlStringRGB(entrada.colorLeds);
            serial.EnviarComando("LED:" + indicesStr + ":" + colorHex);
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

    IEnumerator FinalizarExperiencia()
    {
        Debug.Log("Todos los pares fueron completados. Iniciando secuencia final.");

        float duracionAudio = database.audioFinal != null ? database.audioFinal.length : 0f;
        float duracionMusica = database.musicaFinal != null ? database.musicaFinal.length : 0f;
        float duracionTotal = Mathf.Max(duracionAudio, duracionMusica);

        if (serial != null)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(database.colorAnimacionFinal);
            int duracionMs = Mathf.RoundToInt(duracionTotal * 1000f);
            serial.EnviarComando("FINAL:" + colorHex + ":" + duracionMs);
        }

        if (audioSource != null && database.audioFinal != null)
        {
            audioSource.clip = database.audioFinal;
            audioSource.Play();
        }

        if (musicaSource != null && database.musicaFinal != null)
        {
            musicaSource.clip = database.musicaFinal;
            musicaSource.Play();
        }

        yield return new WaitForSeconds(duracionTotal);
    }

    void OnDisable()
    {
        if (serial != null)
        {
            serial.OnLineaRecibida -= ProcesarId;
            serial.OnConectado -= ApagarLedsAlConectar;
        }
    }
}