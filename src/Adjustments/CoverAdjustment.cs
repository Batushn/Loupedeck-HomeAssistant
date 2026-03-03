namespace Loupedeck.HomeAssistantByBatuPlugin.Adjustments
{
    using System;

    public class CoverAdjustment : PluginDynamicAdjustment
    {
        private new HomeAssistantByBatuPlugin Plugin => (HomeAssistantByBatuPlugin)base.Plugin;

        private AdjustmentDebouncer<Int32> _debouncer;

        public CoverAdjustment()
            : base(hasReset: true)
        {
            this.IsWidget = true;
        }

        protected override Boolean OnLoad()
        {
            _debouncer = new AdjustmentDebouncer<Int32>(this.FlushPosition, 350);
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

            var entities = this.Plugin.HaClient.GetEntitiesByDomain("cover");
            foreach (var entity in entities)
            {
                this.AddParameter(entity.EntityId, entity.FriendlyName, "Covers");
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

            var currentPos = _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetPosition();

            _debouncer.Accumulate(actionParameter, currentPos,
                val => Math.Clamp(val + (diff * 5), 0, 100));

            this.AdjustmentValueChanged(actionParameter);
            this.ActionImageChanged(actionParameter);
        }

        private void FlushPosition(String entityId, Int32 position)
        {
            this.Plugin.HaClient?.CallServiceAsync("cover", "set_cover_position", entityId,
                new { position });
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

            var service = entity.State == "open" ? "close_cover" : "open_cover";
            this.Plugin.HaClient.CallServiceAsync("cover", service, actionParameter);
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

            var pos = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetPosition();

            return $"{pos}%";
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

            var pos = _debouncer != null && _debouncer.TryGetPending(actionParameter, out var pending)
                ? pending
                : entity.GetPosition();

            var isOpen = entity.State == "open";
            return IconHelper.CreateAdjustmentImage(imageSize, entity.FriendlyName, $"{pos}%", isOpen);
        }

        private void OnEntityStateChanged(Object sender, HaStateChangedEventArgs e)
        {
            if (e.NewState?.Domain == "cover")
            {
                this.AdjustmentValueChanged(e.EntityId);
                this.ActionImageChanged(e.EntityId);
            }
        }
    }
}
