using System;
using System.Collections.Generic;
using UnityEngine;

namespace IRIS.SceneLoader
{

	public class IRISTransform
	{
		public List<float> pos;
		public List<float> rot;
		public List<float> scale;

		public Vector3 GetPos()
		{
			return new Vector3(pos[0], pos[1], pos[2]);
		}

		public Quaternion GetRot()
		{
			return new Quaternion(rot[0], rot[1], rot[2], rot[3]);
		}

		public Vector3 GetScale()
		{
			return new Vector3(scale[0], scale[1], scale[2]);
		}

	}

	public class SimAsset
	{
		public string name;
	}

	public class SimVisual : SimAsset
	{
		public string type;
		public SimMesh mesh;
		public SimMaterial material;
		public IRISTransform trans;
	}


	public class SimObject : SimAsset
	{
		public IRISTransform trans;
	}

	public class SimScene : SimAsset
	{
		// public SimObject root;
	}

	

	
	public class SimMesh : SimAsset
	{
		public string hash;
		public List<int> indicesLayout;
		public List<int> verticesLayout;
		public List<int> normalsLayout;
		public List<int> uvLayout;

	}

	
	public class SimMaterial : SimAsset
	{
		public string hash;
		public List<float> color;
		public List<float> emissionColor;
		public float specular;
		public float shininess;
		public float reflectance;
		public SimTexture texture;
	}

	
	public class SimTexture : SimAsset
	{
		public string hash;
		public int width;
		public int height;
		public string textureType;
		public List<float> textureScale;
	}
}