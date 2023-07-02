namespace TeleportationNetwork
{
    public class HudCircleSettings
    {
        /// <summary>
        /// Circle color
        /// </summary>
        public int Color { get; set; } = 0xCCCCCC;

        /// <summary>
        /// How quickly circle fades in (seconds)
        /// </summary>
        public float AlphaIn { get; set; } = 0.2f;

        /// <summary>
        /// How quickly circle fades out (seconds)
        /// </summary>
        public float AlphaOut { get; set; } = 0.4f;

        public int MaxSteps { get; set; } = 16;
        public float OuterRadius { get; set; } = 24;
        public float InnerRadius { get; set; } = 18;
    }
}
