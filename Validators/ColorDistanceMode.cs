namespace SoftConsulting.ColorPicker.Validators
{
    public enum ColorDistanceMode
    {
        /// <summary>The value must be at most the configured distance away from the target color.</summary>
        Within,

        /// <summary>The value must be further than the configured distance, to keep away from the target color.</summary>
        Outside,
    }
}
