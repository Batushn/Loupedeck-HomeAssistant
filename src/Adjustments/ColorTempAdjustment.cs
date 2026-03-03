namespace Loupedeck.HomeAssistantByBatuPlugin.Adjustments
{
    using System;

    public class ColorTempAdjustment : PluginDynamicAdjustment
    {
        private new HomeAssistantByBatuPlugin Plugin => (HomeAssistantByBatuPlugin)base.Plugin;

        private AdjustmentDebouncer<Int32> _debouncer;

        public ColorTempAdjustment()
            : base(hasReset: true)
        {
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            _debouncer = new AdjustmentDebouncer<Int32>(this.FlushColorTemp, 350);
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

            var entities = this.Plugin.HaClient.GetEntitiesByDomain("light");
            foreach (var entity in entities)
            {
                if (entity.SupportsColorTemp())
                {
                    this.AddParameter(entity.EntityId, entity.FriendlyName, "Color Temp");
                }
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

            var (min, max) = entity.GetColorTempRange();
            var range = max - min;
            var step = Math.Max(1, range / 30);

            var currentMireds = _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetColorTemp();

            if (currentMireds <= 0)
            {
                currentMireds = (min + max) / 2;
            }

            _debouncer.Accumulate(actionParameter, currentMireds,
                val => Math.Clamp(val + (diff * step), min, max));

            this.AdjustmentValueChanged(actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        private void FlushColorTemp(String entityId, Int32 mireds)
        {
            if (this.Plugin.HaClient == null)
            {
                return;
            }

            this.Plugin.HaClient.CallServiceAsync("light", "turn_on", entityId,
                new { color_temp = mireds, transition = 0.3 });
        }

        protected override void RunCommand(String actionParameter)
        {
            if (String.IsNullOrEmpty(actionParameter) || this.Plugin.HaClient == null)
            {
                return;
            }

            this.Plugin.HaClient.CallServiceAsync("light", "toggle", actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            if (String.IsNullOrEmpty(actionParameter))
            {
                return "";
            }

            var entity = this.Plugin.HaClient?.GetEntity(actionParameter);
            if (entity == null || !entity.IsOn)
            {
                return "OFF";
            }

            return FormatColorTemp(actionParameter, entity);
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

            var isOn = entity.IsOn;
            var valueText = isOn ? FormatColorTemp(actionParameter, entity) : "OFF";
            var brightnessPct = isOn ? entity.GetBrightnessPercent() : 0;

            return IconHelper.CreateColorTempImage(imageSize, entity.FriendlyName, valueText, isOn,
                GetWarmthFactor(actionParameter, entity), brightnessPct);
        }

        private String FormatColorTemp(String entityId, HaEntity entity)
        {
            Int32 mireds;
            if (_debouncer != null && _debouncer.TryGetPending(entityId, out var pending))
            {
                mireds = pending;
            }
            else
            {
                mireds = entity.GetColorTemp();
            }

            if (mireds <= 0)
            {
                return "--";
            }

            var kelvin = (Int32)Math.Round(1000000.0 / mireds);
            return $"{kelvin}K";
        }

        private Double GetWarmthFactor(String entityId, HaEntity entity)
        {
            Int32 mireds;
            if (_debouncer != null && _debouncer.TryGetPending(entityId, out var pending))
            {
                mireds = pending;
            }
            else
            {
                mireds = entity.GetColorTemp();
            }

            if (mireds <= 0)
            {
                return 0.5;
            }

            var (min, max) = entity.GetColorTempRange();
            if (max <= min)
            {
                return 0.5;
            }

            return Math.Clamp((mireds - min) / (Double)(max - min), 0, 1);
        }

        private void OnEntityStateChanged(Object sender, HaStateChangedEventArgs e)
        {
            if (e.NewState?.Domain == "light")
            {
                this.AdjustmentValueChanged(e.EntityId);
                this.ActionImageChanged(e.EntityId);
            }
        }
    }
}
