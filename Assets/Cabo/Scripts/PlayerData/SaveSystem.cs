using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
public static class SaveSystem
{
    public static void SavePlayer(Player player)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Application.persistentDataPath + "/player.cabo";
        FileStream stream = new FileStream(path, FileMode.Create);
        formatter.Serialize(stream, player);
        stream.Close();
    }

    public static Player LoadPlayer()
    {
        string path = Application.persistentDataPath + "/player.cabo";
        if (File.Exists(path))
    {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);
            Player player = formatter.Deserialize(stream) as Player;
            stream.Close();
            return player;
        }
        else
    {
            Debug.LogError("Save file not found in " + path);
            return null;
        }
    }
}
