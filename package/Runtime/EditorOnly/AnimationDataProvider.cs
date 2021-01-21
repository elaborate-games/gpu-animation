﻿#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Elaborate.AnimationBakery
{
	public static class AnimationDataProvider
	{
		public static List<AnimationTransformationData> GetAnimations(
			Animator animator, List<AnimationClip> animationClips, SkinnedMeshRenderer skinnedMeshRenderer,
			int skip, float frameRate = -1
		)
		{
			var bones = skinnedMeshRenderer.bones;

			var boneData = GetData(skinnedMeshRenderer);
			var bonesInfo = new Dictionary<Transform, SkinnedMesh_BoneData>();
			for (var i = 0; i < boneData.Count; i++)
				bonesInfo.Add(boneData[i].Bone, boneData[i]);

			var result = new List<AnimationTransformationData>();
			var clips = animationClips; // //AnimationUtility.GetAnimationClips(animator.gameObject);
			foreach (var clip in clips)
			{
				// make sure not to add one clip multiple times
				if (result.Any(r => r.Clip == clip)) continue;

				var data = SampleAnimationData(skinnedMeshRenderer.sharedMesh, animator, skinnedMeshRenderer.rootBone, bones, bonesInfo, clip, skip, out int fps, frameRate);
				result.Add(new AnimationTransformationData(clip, data, fps));
			}

			return result;
		}

		private static BoneTransformationData[] SampleAnimationData(
			Mesh mesh, 
			Animator animatedObject,
			Transform rootBone,
			Transform[] bones,
			Dictionary<Transform, SkinnedMesh_BoneData> bonesInfo,
			AnimationClip clip, int skip, 
			out int sampledFramesPerSecond,
			float frameRate = -1
		)
		{
			if (!AnimationClipUtility.GetData(animatedObject, clip, out AnimationClipData data))
			{
				Debug.LogError("Failed getting data from " + clip + ", " + animatedObject, animatedObject);
				sampledFramesPerSecond = 0;
				return null;
			}

			// 1: save bind transformations of bones
			var transformStates = GetTransformationState(bones);
			// var rootState = rootBone.GetTransformationState();
			// var prevParent = rootBone.parent;
			// rootBone.position = Vector3.zero;
			// rootBone.rotation = Quaternion.identity;
			// rootBone.localScale = Vector3.one;

			// 2: sample transformations in clip
			var transformations = SampleAndStoreAnimationClipData(animatedObject, clip, mesh, bones, bonesInfo, data, skip, frameRate, out sampledFramesPerSecond);

			// 3: restore transformation state
			RestoreTransformationState(transformStates);
			// rootBone.RestoreTransformationState(rootState);

			return transformations;
		}

		private static BoneTransformationData[] SampleAndStoreAnimationClipData(
			Animator animator, AnimationClip clip,
			Mesh mesh, IReadOnlyList<Transform> bones,
			Dictionary<Transform, SkinnedMesh_BoneData> bonesInfo,
			AnimationClipData data, int skip, float frameRate,
			out int sampledFramesPerSecond
			)
		{
			var boneTransformations = new Dictionary<Transform, BoneTransformationData>();

			frameRate = frameRate <= 0 ? data.FrameRate : frameRate;

			skip = Mathf.Max(0, skip);
			skip += 1;
			
			sampledFramesPerSecond = Mathf.FloorToInt((float) frameRate / skip);

			var duration = data.Duration;
			var frames = duration * frameRate;
			
			AnimationMode.StartAnimationMode();
			for (var i = 0; i < frames; i++)
			{
				if (skip > 1 && i % skip != 0) continue;

				var frame = i;
				var time = ((float) frame / frames) * duration;
				
				AnimationMode.BeginSampling();
				AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);

				// set pose
				// foreach (var curveData in data.Curves)
				// {
				// 	var bone = curveData.Key;
				// 	var curve = curveData.Value;
				//
				// 	if (curve.HasPositionKeyframes)
				// 		bone.localPosition = curve.Position(time);
				// 	if (curve.HasRotationKeyframes)
				// 		bone.localRotation = curve.Rotation(time);
				// 	if (curve.HasScaleKeyframes)
				// 		bone.localScale = curve.Scale(time);
				// }

				// add keyframes for all bones for now...
				foreach (var kvp in bonesInfo)
				{
					var info = kvp.Value;
					var index = info.Index;
					var bone = bones[index];

					var pos = bone.position;
					var rot = bone.rotation;
					var scale = bone.lossyScale;

					var boneMatrix = Matrix4x4.TRS(pos, rot, scale);
					// bindposes are already inverted!!!
					var bp = mesh.bindposes[index];
					boneMatrix *= bp;

					if (boneTransformations.ContainsKey(bone) == false)
						boneTransformations.Add(bone, new BoneTransformationData(bone.name, bone, info.Index, new List<BoneTransformation>()));

					boneTransformations[bone].Transformations.Add(new BoneTransformation(time, boneMatrix, scale != Vector3.one));
				}
				
				AnimationMode.EndSampling();
			}
			AnimationMode.StopAnimationMode();
			

			return boneTransformations.Values.ToArray();
			return boneTransformations.Values.OrderBy(e => e.BoneIndex).ToArray();
		}


		public static List<SkinnedMesh_BoneData> GetData(SkinnedMeshRenderer renderer)
		{
			var boneHierarchy = new List<SkinnedMesh_BoneData>();

			//Debug.LogWarning("make sure we start with the parent of the skinned mesh root bone because thats how it is stored in the animation clip as well");
			// var rootBone = renderer.rootBone;
			var bones = renderer.bones;

			for (int i = 0; i < bones.Length; i++)
			{
				var bone = bones[i];
				var newData = new SkinnedMesh_BoneData()
				{
					Bone = bone,
					Index = i,
				};
				boneHierarchy.Add(newData);
			}

			return boneHierarchy;
		}


		private static void RestoreTransformationState(Dictionary<Transform, TransformState> states)
		{
			foreach (var kvp in states)
			{
				var transform = kvp.Key;
				var state = kvp.Value;
				RestoreTransformationState(transform, state);
			}
		}

		private static void RestoreTransformationState(this Transform transform, TransformState state)
		{
			transform.localPosition = state.LocalPosition;
			transform.localRotation = state.LocalRotation;
			transform.localScale = state.LocalScale;
		}

		private static Dictionary<Transform, TransformState> GetTransformationState(IEnumerable<Transform> bones)
		{
			return bones.ToDictionary(bone => bone, bone => bone.GetTransformationState());
		}

		private static TransformState GetTransformationState(this Transform bone)
		{
			return new TransformState()
			{
				Position = bone.position,
				LocalPosition = bone.localPosition,
				Rotation = bone.rotation,
				LocalRotation = bone.localRotation,
				LossyScale = bone.lossyScale,
				LocalScale = bone.localScale
			};
		}


		
		// private static TransformState CaptureTransformAndSetParentToCenter(Transform rootBone)
		// {
		// 	
		// }
		//
		// private static void SetPreviousRoot(Transform rootBone)
		// {
		// 	
		// }
	}
}
#endif