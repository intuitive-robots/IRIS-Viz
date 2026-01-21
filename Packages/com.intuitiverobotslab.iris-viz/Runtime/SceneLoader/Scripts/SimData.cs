using MessagePack;
using UnityEngine;

namespace IRIS.SceneLoader
{
    // Use [MessagePackObject] for every class/struct you want to serialize
    // Use [Key(int)] to define the index in the binary stream (efficient)

    [MessagePackObject]
    public class SimTransform
    {
        [Key(0)]
        public float[] pos; // Changed from List to Array for performance
        [Key(1)]
        public float[] rot;
        [Key(2)]
        public float[] scale;

        public Vector3 GetPos() => new Vector3(-pos[1], pos[2], pos[0]);
        public Quaternion GetRot() => new Quaternion(rot[2], -rot[3], -rot[1], rot[0]);
        public Vector3 GetScale() => new Vector3(scale[1], scale[2], scale[0]);
    }

    [MessagePackObject]
    public class SimVisual
    {
        [Key(0)]
        public string name;
        [Key(1)]
        public string type;
        [Key(2)]
        public SimMesh mesh;
        [Key(3)]
        public SimMaterial material;
        [Key(4)]
        public SimTransform trans;
    }

    [MessagePackObject]
    public class SimObject
    {
        [Key(0)]
        public string name;
        [Key(1)]
        public SimTransform trans;
        [Key(2)]
        public SimVisual[] visuals;
        [Key(3)]
        public SimObject[] children;
    }

    [MessagePackObject]
    public class SimMesh
    {
        [Key(0)]
        public byte[] indices;
        [Key(1)]
        public byte[] vertices;
        [Key(2)]
        public byte[] normals;
        [Key(3)]
        public byte[] uv;
    }

    [MessagePackObject]
    public class SimMaterial
    {
        [Key(0)]
        public string hash;
        [Key(1)]
        public float[] color;
        [Key(2)]
        public float[] emissionColor;
        [Key(3)]
        public float specular;
        [Key(4)]
        public float shininess;
        [Key(5)]
        public float reflectance;
        [Key(6)]
        public SimTexture texture;
    }

    [MessagePackObject]
    public class SimTexture
    {
        [Key(0)]
        public string hash;
        [Key(1)]
        public int width;
        [Key(2)]
        public int height;
        [Key(3)]
        public string textureType;
        [Key(4)]
        public float[] textureScale;
        [Key(5)]
        public byte[] textureData;
    }

    [MessagePackObject]
    public class SimScene
    {
        [Key(0)]
        public string name;
    }

}
