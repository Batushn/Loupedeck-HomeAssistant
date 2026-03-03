namespace Loupedeck.HomeAssistantByBatuPlugin.Adjustments
{
    using System;

    public class ClimateAdjustment : PluginDynamicAdjustment
    {
        private new HomeAssistantByBatuPlugin Plugin => (HomeAssistantByBatuPlugin)base.Plugin;

        private AdjustmentDebouncer<Double> _debouncer;

        public ClimateAdjustment()
            : base(hasReset: false)
        {
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            _debouncer = new AdjustmentDebouncer<Double>(this.FlushTemperature, 350);
            this.Plugin.HaStatesLoaded += this.OnStatesLoaded;
            this.Plugin.EntityStateChanged += this.OnEntityStateChanged;
            return true;
        }

        protected override Boolean OnUnload()
        {
            this.Plugin.HaStatesLoaded -= this.OnStatesLoaded;
            this.Plugin.EntityStateChanged -= this.OnEntityStateChanged;
            _debouncer?.Dispose();
            return true;
        }

        private void OnStatesLoaded(Object sender, EventArgs e) => this.RefreshParameters();

        private void RefreshParameters()
        {
            if (this.Plugin.HaClient == null)
            {
                return;
            }

            var entities = this.Plugin.HaClient.GetEntitiesByDomain("climate");
            foreach (var entity in entities)
            {
                this.AddParameter(entity.EntityId, entity.FriendlyName, "Climate");
            }

            this.ParametersChanged();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (String.IsNullOrEmpty(actionParameter) || this.Plugin.HaClient == null)
            {
                return;
            }

            var entity = this.Plugin.HaClient.GetEntity(actionParameter);
            if (entity == null)
            {
                return;
            }

            var currentTemp = _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetTemperature();

            _debouncer.Accumulate(actionParameter, currentTemp,
                val => val + (diff * 0.5));

            this.AdjustmentValueChanged(actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        private void FlushTemperature(String entityId, Double temperature)
        {
            this.Plugin.HaClient?.CallServiceAsync("climate", "set_temperature", entityId,
                new { temperature });
        }

        protected override void RunCommand(String actionParameter)
        {
            if (String.IsNullOrEmpty(actionParameter) || this.Plugin.HaClient == null)
            {
                return;
            }

            var entity = this.Plugin.HaClient.GetEntity(actionParameter);
            if (entity == null)
            {
                return;
            }

            var service = entity.State == "off" ? "turn_on" : "turn_off";
            this.Plugin.HaClient.CallServiceAsync("climate", service, actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            if (String.IsNullOrEmpty(actionParameter))
            {
                return "";
            }

            var entity = this.Plugin.HaClient?.GetEntity(actionParameter);
            if (entity == null)
            {
                return "";
            }

            var temp = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetTemperature();

            return temp > 0 ? $"{temp:F1}\u00B0" : entity.State;
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (String.IsNullOrEmpty(actionParameter))
            {
                return IconHelper.CreateOfflineImage(imageSize);
            }

            var entity = this.Plugin.HaClient?.GetEntity(actionParameter);
            if (entity == null)
            {
                return IconHelper.CreateOfflineImage(imageSize);
            }

            var isOn = entity.State != "off";

            var temp = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetTemperature();

            var valueText = temp > 0 ? $"{temp:F1}\u00B0" : entity.State;
            return IconHelper.CreateAdjustmentImage(imageSize, entity.FriendlyName, valueText, isOn);
        }

        private void OnEntityStateChanged(Object sender, HaStateChangedEventArgs e)
        {
            if (e.NewState?.Domain == "climate")
            {
                this.AdjustmentValueChanged(e.EntityId);
                this.ActionImageChanged(e.EntityId);
            }
        }
    }
}
