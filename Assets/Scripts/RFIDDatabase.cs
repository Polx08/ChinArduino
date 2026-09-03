using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RFIDDatabase", menuName = "RFID/Database")]
public class RFIDDatabase : ScriptableObject
{
    [Header("Parte 1")]
    public List<RFIDEntry> seccion1 = new List<RFIDEntry>();
    public AudioClip audioFinalSeccion1;

    [Header("Parte 2")]
    public List<RFIDEntry> seccion2 = new List<RFIDEntry>();
    public AudioClip audioFinal;

    public static RFIDEntry BuscarPorId(List<RFIDEntry> lista, string id)
    {
        foreach (var entrada in lista)
        {
            if (entrada.id.Trim().Equals(id.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return entrada;
            }
        }
        return null;
    }
}

[System.Serializable]
public class RFIDEntry
{
    public string nombre;
    public string id;
    public AudioClip audio;
    public Sprite imagen;
    public KeyCode teclaDebug = KeyCode.None;

    [Header("Duracion imagen")]
    [Tooltip("Si es 0 o menor, se usa la duracion del audio")]
    public float duracionImagen = -1f;

    [Header("Anillo LED WS2812 (16 leds)")]
    public List<int> ledsIndices = new List<int>();
    public Color colorLeds = Color.white;
}