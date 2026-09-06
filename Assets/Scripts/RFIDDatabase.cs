using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RFIDDatabase", menuName = "RFID/Database")]
public class RFIDDatabase : ScriptableObject
{
    public List<TagPair> pares = new List<TagPair>();
    public AudioClip audioFinal;

    public RFIDEntry BuscarEntradaPorId(string id)
    {
        foreach (var par in pares)
        {
            if (par.principal != null && par.principal.id.Trim().Equals(id.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return par.principal;
            }

            if (par.pareja != null && par.pareja.id.Trim().Equals(id.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return par.pareja;
            }
        }
        return null;
    }
}

[System.Serializable]
public class TagPair
{
    public string nombre;
    public RFIDEntry principal;
    public RFIDEntry pareja;
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