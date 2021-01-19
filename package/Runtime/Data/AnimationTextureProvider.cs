﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elaborate.AnimationBakery
{
	[Serializable]
	public class BakedData : IDisposable
	{
		public Texture Texture;

		public void Dispose()
		{
			if(Texture is RenderTexture rt && rt) rt.Release();
		}
	}
	
	[Serializable]
	public class AnimationTextureData : BakedData
	{
		public List<Clip> Animations;

		[Serializable]
		public struct Clip
		{
			public int IndexStart;
			public int Length;
			public int Stride => sizeof(int) * 2;

			public override string ToString()
			{
				return "Start=" + IndexStart + ", Length=" + Length;
			}
		}
	}

	[Serializable]
	public class MeshSkinningData : BakedData
	{
		public Mesh Mesh;
	}

	public static class AnimationTextureProvider
	{
		public static AnimationTextureData BakeAnimation(IEnumerable<AnimationTransformationData> animationData, ComputeShader shader)
		{
			var matrixData = new List<Matrix4x4>();
			var clipInfos = new List<AnimationTextureData.Clip>();
			var anyScaled = false;
			foreach (var anim in animationData)
			{
				var clip = new AnimationTextureData.Clip();
				clip.IndexStart = matrixData.Count;
				foreach (var boneData in anim.BoneData)
				{
					foreach (var frame in boneData.Transformations)
					{
						// Debug.Log(frame.Matrix);
						matrixData.Add(frame.Matrix);
						if (frame.Scaled) anyScaled = true;
					}
				}

				clip.Length = matrixData.Count - clip.IndexStart;
				Debug.Log("Clip \t" + clipInfos.Count + "\t" + clip);
				clipInfos.Add(clip);
			}

			var textureSize = Mathf.CeilToInt(Mathf.Sqrt(matrixData.Count));
			var texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGBFloat); // TODO: try ARGBHalf
			texture.enableRandomWrite = true;
			texture.useMipMap = false;
			texture.filterMode = FilterMode.Point;
			texture.Create();
			var res = new AnimationTextureData();
			res.Texture = texture;
			res.Animations = clipInfos;

			Debug.Log(clipInfos.Count + " clip(s), data is " + matrixData.Count + " matrices: 4x4 = " + (matrixData.Count * 4) + " pixel TextureSize: " +
			          texture.width + "x" + texture.height + ", Need Scale? " + anyScaled);

			var kernel = shader.FindKernel("BakeAnimationTexture_Float4");
			using (var matrixBuffer = new ComputeBuffer(matrixData.Count, sizeof(float) * 4 * 4))
			{
				matrixBuffer.SetData(matrixData);
				shader.SetBuffer(kernel, "Matrices", matrixBuffer);
				shader.SetTexture(kernel, "Texture", texture);
				shader.Dispatch(kernel, Mathf.CeilToInt(matrixData.Count / 32f), 1, 1);
			}

			return res;
		}


		public static MeshSkinningData BakeSkinning(Mesh mesh, ComputeShader shader)
		{
			var res = new MeshSkinningData();
			res.Mesh = mesh;
			
			var kernel = shader.FindKernel("BakeBoneWeights");
			using (var buffer = CreateVertexBoneWeightBuffer(mesh))
			{
				var textureSize = Mathf.CeilToInt(Mathf.Sqrt(buffer.count));
				var texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RGHalf); // TODO: try ARGBHalf
				texture.enableRandomWrite = true;
				texture.useMipMap = false;
				texture.filterMode = FilterMode.Point;
				res.Texture = texture;
				
				shader.SetBuffer(kernel, "Weights", buffer);
				shader.Dispatch(kernel, Mathf.CeilToInt(buffer.count / 32f), 1, 1);
			}
			return res;
		}

		private struct BoneWeight
		{
			public int BoneIndex;
			public float Weight;

			public static int Size => sizeof(int) + sizeof(float);
		}

		private static ComputeBuffer CreateVertexBoneWeightBuffer(Mesh mesh)
		{
			// because each element only holds one index of a bone 
			// and the weight for this bone
			// other than the UnityEngine.BoneWeight 
			// which holds: boneWeight0, boneWeight1 and so on
			var boneWeights = mesh.boneWeights;
			var count = boneWeights.Length * 4;
			var weightBuffer = new ComputeBuffer(count, BoneWeight.Size);
			var boneWeightData = new BoneWeight[count];
			for (var i = 0; i < boneWeights.Length; i++)
			{
				var di = i * 4;

				boneWeightData[di].BoneIndex = boneWeights[i].boneIndex0;
				boneWeightData[di].Weight = boneWeights[i].weight0;

				boneWeightData[di + 1].BoneIndex = boneWeights[i].boneIndex1;
				boneWeightData[di + 1].Weight = boneWeights[i].weight1;

				boneWeightData[di + 2].BoneIndex = boneWeights[i].boneIndex2;
				boneWeightData[di + 2].Weight = boneWeights[i].weight2;

				boneWeightData[di + 3].BoneIndex = boneWeights[i].boneIndex3;
				boneWeightData[di + 3].Weight = boneWeights[i].weight3;
			}

			weightBuffer.SetData(boneWeightData);
			return weightBuffer;
		}
	}
}