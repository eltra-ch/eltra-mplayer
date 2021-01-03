using System;
using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.VolumeControl.Behaviors
{
    class MasterVolumeSliderBehavior : Behavior<Slider>
    {
        Slider _control;
        VolumeControlViewModel _viewModel;

        protected override void OnAttachedTo(Slider control)
        {
            _control = control;
            _viewModel = control.BindingContext as VolumeControlViewModel;

            control.BindingContextChanged += OnBindingContextChanged;
            control.ValueChanged += OnValueChanged;

            base.OnAttachedTo(control);
        }

        private void OnValueChanged(object sender, ValueChangedEventArgs e)
        {
            if(_viewModel!=null)
            {
                _viewModel.SliderVolumeValueChanged(e.NewValue);
            }
        }

        private void OnBindingContextChanged(object sender, EventArgs e)
        {
            if (_control.BindingContext is VolumeControlViewModel model)
            {
                _viewModel = model;
            }
        }
    }
}
