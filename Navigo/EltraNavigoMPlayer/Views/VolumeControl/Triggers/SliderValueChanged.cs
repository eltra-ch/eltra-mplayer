using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.VolumeControl.Triggers
{
    class SliderValueChanged : TriggerAction<Slider>
    {
        protected override void Invoke(Slider sender)
        {
            if (sender.BindingContext is VolumeControlViewModel viewModel)
            {
                viewModel.SliderVolumeValueChanged(sender.Value);
            }
        }
    }
}
