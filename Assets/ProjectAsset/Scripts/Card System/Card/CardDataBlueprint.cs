using UnityEngine;

namespace ProjectTimeline.Timeline
{
    /// <summary>
    /// ScriptableObject blueprint representing static card metadata.
    /// Holds energy cost, visual descriptors, and the action node payload.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCardBlueprint", menuName = "Project Timeline/Card Blueprint")]
    public class CardDataBlueprint : ScriptableObject
    {
        [Header("Card Metadata")]
        public string cardId;
        public string cardName;
        public CardType cardType;
        [TextArea(2, 5)]
        public string cardDescription;

        [Header("Usage Costs")]
        public int energyCost;

        [Header("Visual Elements")]
        public Sprite icon;

        [Header("Action Configuration blueprint")]
        [Tooltip("The combat action node that will be placed on the timeline when this card is played.")]
        public ActionNodeData actionBlueprint;
    }
}
