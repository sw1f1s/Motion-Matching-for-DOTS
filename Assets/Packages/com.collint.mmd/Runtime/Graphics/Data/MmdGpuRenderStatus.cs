namespace ColInt.MMD.Graphics.Data {
	public enum MmdGpuRenderStatus : byte {
		Loading,
		WaitingForPose,
		Ready,
		InvalidPrefab,
		IncompatibleSkeleton,
		InvalidRig,
		BindingFailed,
		Unloaded
	}
}
