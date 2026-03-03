namespace Loupedeck.HomeAssistantByBatuPlugin.Adjustments
{
    using System;
    using System.Text.Json;

    public class MediaVolumeAdjustment : PluginDynamicAdjustment
    {
        private new HomeAssistantByBatuPlugin Plugin => (HomeAssistantByBatuPlugin)base.Plugin;

        private AdjustmentDebouncer<Double> _debouncer;

        public MediaVolumeAdjustment()
            : base(hasReset: true)
        {
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            _debouncer = new AdjustmentDebouncer<Double>(this.FlushVolume, 350);
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

            var entities = this.Plugin.HaClient.GetEntitiesByDomain("media_player");
            foreach (var entity in entities)
            {
                this.AddParameter(entity.EntityId, entity.FriendlyName, "Media###Volume");
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

            var currentVolume = _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : GetVolumeLevel(entity);

            _debouncer.Accumulate(actionParameter, currentVolume,
                val => Math.Clamp(val + (diff * 0.03), 0.0, 1.0));

            this.AdjustmentValueChanged(actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        private void FlushVolume(String entityId, Double volume)
        {
            this.Plugin.HaClient?.CallServiceAsync("media_player", "volume_set", entityId,
                new { volume_level = volume });
        }

        protected override void RunCommand(String actionParameter)
        {
            if (String.IsNullOrEmpty(actionParameter) || this.Plugin.HaClient == null)
            {
                return;
            }

            this.Plugin.HaClient.CallServiceAsync("media_player", "volume_mute", actionParameter,
                new { is_volume_muted = true });
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

            var vol = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : GetVolumeLevel(entity);

            return $"{(Int32)(vol * 100)}%";
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

            var vol = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : GetVolumeLevel(entity);

            var isOn = entity.State != "off" && entity.State != "unavailable";
            var valueText = $"Vol: {(Int32)(vol * 100)}%";

            return IconHelper.CreateAdjustmentImage(imageSize, entity.FriendlyName, valueText, isOn);
        }

        private static Double GetVolumeLevel(HaEntity entity)
        {
            try
            {
                if (entity.Attributes.ValueKind == JsonValueKind.Object &&
                    entity.Attributes.TryGetProperty("volume_level", out var v))
                {
                    return v.GetDouble();
                }
            }
            catch { }
            return 0;
        }

        private void OnEntityStateChanged(Object sender, HaStateChangedEventArgs e)
        {
            if (e.NewState?.Domain == "media_player")
            {
                this.AdjustmentValueChanged(e.EntityId);
                this.ActionImageChanged(e.EntityId);
            }
        }
    }
}
