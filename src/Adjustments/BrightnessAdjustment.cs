namespace Loupedeck.HomeAssistantByBatuPlugin.Adjustments
{
    using System;

    public class BrightnessAdjustment : PluginDynamicAdjustment
    {
        private new HomeAssistantByBatuPlugin Plugin => (HomeAssistantByBatuPlugin)base.Plugin;

        private AdjustmentDebouncer<Int32> _debouncer;

        public BrightnessAdjustment()
            : base(hasReset: true)
        {
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            _debouncer = new AdjustmentDebouncer<Int32>(this.FlushBrightness, 350);
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
                this.AddParameter(entity.EntityId, entity.FriendlyName, "Dimmers");
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

            var currentPct = _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetBrightnessPercent();

            var step = Math.Abs(diff) > 1 ? 5 : 3;

            _debouncer.Accumulate(actionParameter, currentPct,
                val => Math.Clamp(val + (diff * step), 0, 100));

            this.AdjustmentValueChanged(actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        private void FlushBrightness(String entityId, Int32 targetPct)
        {
            if (this.Plugin.HaClient == null)
            {
                return;
            }

            if (targetPct <= 0)
            {
                this.Plugin.HaClient.CallServiceAsync("light", "turn_off", entityId);
            }
            else
            {
                var brightness = (Int32)Math.Round(targetPct / 100.0 * 255);
                this.Plugin.HaClient.CallServiceAsync("light", "turn_on", entityId,
                    new { brightness, transition = 0.3 });
            }
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

            if (_debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending))
            {
                return pending <= 0 ? "OFF" : $"{pending}%";
            }

            var entity = this.Plugin.HaClient?.GetEntity(actionParameter);
            if (entity == null || !entity.IsOn)
            {
                return "OFF";
            }

            return $"{entity.GetBrightnessPercent()}%";
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

            Int32 pct;
            Boolean isOn;

            if (_debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending))
            {
                pct = pending;
                isOn = pending > 0;
            }
            else
            {
                pct = entity.GetBrightnessPercent();
                isOn = entity.IsOn;
            }

            var valueText = isOn ? $"{pct}%" : "OFF";
            return IconHelper.CreateAdjustmentImage(imageSize, entity.FriendlyName, valueText, isOn, pct);
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
