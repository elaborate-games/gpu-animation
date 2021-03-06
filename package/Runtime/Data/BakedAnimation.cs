﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace needle.GpuAnimation
{
	[Serializable]
	public class BakedModel
	{
		public BakedMeshSkinningData Skinning;
		public BakedAnimationData Animations;
		public Mesh Mesh => Skinning?.Mesh;
		public bool IsValid => Mesh && Animations?.ClipsInfos?.Count > 0 && Skinning.Texture && Animations.Texture;

		public BakedModel(BakedMeshSkinningData skinning, BakedAnimationData animations)
		{
			this.Skinning = skinning;
			this.Animations = animations;
		}

		public IEnumerable<Texture> EnumerateTextures()
		{
			yield return Skinning?.Texture;
			yield return Animations?.Texture;
		}
	}

	[CreateAssetMenu(menuName = "Animation/Baked Animation", order = -1000)]
	public class BakedAnimation : ScriptableObject
	{
		public IReadOnlyList<BakedModel> Models
		{
			get
			{
#if UNITY_EDITOR || !UNITY_WEBGL
				if (_bakes == null || !_bakes.All(b => b.IsValid))
				{
					BakeAnimations();
				}
#endif
				return _bakes;
			}
		}

		public bool HasBakedAnimation => Models != null && ClipsCount > 0;
		public int ClipsCount => Models?.Sum(b => b?.Animations?.ClipsInfos?.Count ?? 0) ?? 0;


		[SerializeField] private ComputeShader Shader;

		[Header("Input Data")] [SerializeField]
		private GameObject GameObject;

		[SerializeField] private List<AnimationClip> Animations;

		[SerializeField, HideInInspector] private List<BakedModel> _bakes;

		private bool CheckCanBake(bool allowLogs)
		{
			if (!GameObject)
			{
				if (allowLogs) Debug.LogWarning($"Can not bake {this.name}: No GameObject assigned", this);
				return false;
			}

			if (!Animations.Any(a => a))
			{
				if (allowLogs) Debug.LogWarning($"Can not bake {this.name}:  No Animations assigned", this);
				return false;
			}

			return true;
		}

		[ContextMenu("Bake Now")]
		public bool BakeAnimations()
		{
			if (!CheckCanBake(false)) return false;

			if (_bakes == null) _bakes = new List<BakedModel>();
			else _bakes.Clear();

			var instance = Instantiate(GameObject);
			try
			{
				instance.hideFlags = HideFlags.HideAndDontSave;
				var animator = instance.GetComponentInChildren<Animator>();
				var animatedObject = animator ? animator.gameObject : instance;
				var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
				var renderer = renderers.FirstOrDefault(r => r);
				if (renderer)
				{
					var animData = AnimationDataProvider.GetAnimations(animatedObject, Animations, renderer, 0, -1);
					for (var index = 0; index < renderers.Length; index++)
					{
						renderer = renderers[index];
						var skinBake = AnimationTextureProvider.BakeSkinning(renderer.sharedMesh, Shader);
						var animationBake = AnimationTextureProvider.BakeAnimation(animData, Shader);
						_bakes.Add(new BakedModel(skinBake, animationBake));
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				DestroyImmediate(instance);
			}

			return true;
		}


#if UNITY_EDITOR
		[SerializeField] private bool UpdateImmediately = true;
		private int previousHash;
		private int validateCounter;

		private async void OnValidate()
		{
			if(Selection.Contains(this)) 
				CheckCanBake(true);
			
			var count = ++validateCounter;
			await Task.Delay(100);
			while (EditorApplication.isCompiling || EditorApplication.isUpdating) await Task.Delay(100);
			if (validateCounter != count) return;
			
			if (UpdateImmediately)
			{
				if (GameObject)
				{
					var hash = GameObject.GetHashCode() + Animations.Sum(e => e ? e.GetHashCode() : 0);
					var changed = previousHash != hash;
					previousHash = hash;
					if (changed)
						BakeAnimations();
				}
			}
		}


		//
		// private IEnumerable<Object> GetSubAssets()
		// {
		// 	var objs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(this));
		//
		// 	var list = new List<Object>();
		//
		// 	foreach (var o in objs)
		// 	{
		// 		if (AssetDatabase.IsSubAsset(o))
		// 			list.Add(o);
		// 	}
		//
		// 	return list;
		// }
#endif
	}
}