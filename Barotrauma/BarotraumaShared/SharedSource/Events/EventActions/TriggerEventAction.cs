﻿namespace Barotrauma
{
    /// <summary>
    /// Triggers another event (can also trigger things other than scripted events, for example monster events).
    /// </summary>
    class TriggerEventAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "Identifier of the event to trigger.")] 
        public Identifier Identifier { get; set; }

        [Serialize("", IsPropertySaveable.Yes, description: "Tag of the event to trigger.")]
        public Identifier EventTag { get; set; }

        [Serialize(false, IsPropertySaveable.Yes, description: "If set to true, the event will trigger at the beginning of the next round. Useful for e.g. triggering some scripted event in the outpost after you finish a mission.")]
        public bool NextRound { get; set; }

        private bool isFinished;

        public TriggerEventAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element) { }

        public override bool IsFinished(ref string goTo)
        {
            return isFinished;
        }
        public override void Reset()
        {
            isFinished = false;
        }

        public override void Update(float deltaTime)
        {
            if (isFinished) { return; }

            if (GameMain.GameSession?.EventManager != null)
            {
                if (NextRound)
                {
                    GameMain.GameSession.EventManager.QueuedEventsForNextRound.Enqueue(Identifier);
                }
                else
                {
                    EventPrefab eventPrefab = EventPrefab.FindEventPrefab(Identifier, EventTag, ParentEvent.Prefab.ContentPackage);
                    if (eventPrefab != null)
                    {
                        var ev = eventPrefab.CreateInstance(GameMain.GameSession.EventManager.RandomSeed);
                        if (ev != null)
                        {
                            GameMain.GameSession.EventManager.QueuedEvents.Enqueue(ev);                            
                        }
                    }
                }
            }

            isFinished = true;
        }

        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(TriggerEventAction)} -> (EventPrefab: {Identifier.ColorizeObject()})";
        }
    }
}