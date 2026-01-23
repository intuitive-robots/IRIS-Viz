using MessagePack;
using UnityEngine;

namespace IRIS.SceneLoader
{
    // Use [MessagePackObject] for every class/struct you want to serialize
    // Use [Key(int)] to define the index in the binary stream (efficient)

    [MessagePackObject]
    public class SimTransform
    {
        [Key("pos")]
        public float[] pos; // Changed from List to Array for performance
        [Key("rot")]
        public float[] rot;
        [Key("scale")]
        public float[] scale;

        public Vector3 GetPos() => new Vector3(pos[0], pos[1], pos[2]);
        public Quaternion GetRot() => new Quaternion(rot[0], rot[1], rot[2], rot[3]);
        public Vector3 GetScale() => new Vector3(scale[0], scale[1], scale[2]);
    }

    [MessagePackObject]
    public class SimVisual
    {
        [Key("name")]
        public string name;

        [Key("type")]
        public string type;

        [Key("mesh")]
        public SimMesh mesh;

        [Key("material")]
        public SimMaterial material;

        [Key("trans")]
        public SimTransform trans;
    }


    [MessagePackObject]
    public class SimObject
    {
        [Key("name")]
        public string name;
        [Key("parent")]
        public string parent;
        [Key("trans")]
        public SimTransform trans;
        [Key("visuals")]
        public SimVisual[] visuals;
    }

    [MessagePackObject]
    public class SimMesh
    {
        [Key("indices")]
        public byte[] indices;
        [Key("vertices")]
        public byte[] vertices;
        [Key("normals")]
        public byte[] normals;
        [Key("uv")]
        public byte[] uv;
    }

    [MessagePackObject]
    public class SimMaterial
    {
        [Key("color")]
        public float[] color;
        [Key("emissionColor")]
        public float[] emissionColor;
        [Key("specular")]
        public float specular;
        [Key("shininess")]
        public float shininess;
        [Key("reflectance")]
        public float reflectance;
        [Key("texture")]
        public SimTexture texture;
    }

    [MessagePackObject]
    public class SimTexture
    {
        [Key("width")]
        public int width;
        [Key("height")]
        public int height;
        [Key("textureType")]
        public string textureType;
        [Key("textureScale")]
        public float[] textureScale;
        [Key("textureData")]
        public byte[] textureData;
    }

    [MessagePackObject]
    public class LightConfig
    {
        [Key("name")] public string name;
        [Key("lightType")] public string lightType;
        [Key("color")] public float[] color;
        [Key("intensity")] public float intensity;
        [Key("position")] public float[] position;
        [Key("direction")] public float[] direction;
        [Key("range")] public float range;
        [Key("spotAngle")] public float spotAngle;
        [Key("shadowType")] public string shadowType;
    }


    [MessagePackObject]
    public class SimSceneConfig
    {
        [Key("name")]
        public string name;
        [Key("pos")]
        public float[] pos;
        [Key("rot")]
        public float[] rot;
        [Key("scale")]
        public float[] scale;
    }

}
