﻿using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Desynchronized.TNDBS
{
    public class TaleNewsPawnBanished : TaleNewsNegativeIndividual
    {
        public Pawn BanishmentVictim => PrimaryVictim;
        public bool BanishmentIsDeadly { get; }

        public TaleNewsPawnBanished(Pawn victim, bool isBanishedToDie): base(victim, InstigatorInfo.NoInstigator)
        {
            BanishmentIsDeadly = isBanishedToDie;
        }

        protected override void GiveThoughtsToReceipient(Pawn recipient)
        {
            // FileLog.Log("BanishmentVictim: " + BanishmentVictim.Name + "; is he a prisoner? " + BanishmentVictim.IsPrisoner + "; is he a prisoner of the Colony? " + BanishmentVictim.IsPrisonerOfColony);

            if (!recipient.IsCapableOfThought())
            {
                return;
            }

            // Switch for handling Bonded Animal Banished
            if (BanishmentVictim.RaceProps.Animal)
            {
                if (recipient.relations.GetDirectRelation(PawnRelationDefOf.Bond, BanishmentVictim) != null)
                {
                    new IndividualThoughtToAdd(ThoughtDefOf.BondedAnimalBanished, recipient, BanishmentVictim).Add();
                }
            }
            else if (recipient == BanishmentVictim)
            {
                // We have potential here. Next version perhaps.
            }
            else
            {
                ThoughtDef thoughtDefToGain = null;
                if (!BanishmentVictim.IsPrisonerOfColony)
                {
                    if (BanishmentIsDeadly)
                    {
                        thoughtDefToGain = ThoughtDefOf.ColonistBanishedToDie;
                    }
                    else
                    {
                        thoughtDefToGain = ThoughtDefOf.ColonistBanished;
                    }
                }
                else
                {
                    if (BanishmentIsDeadly)
                    {
                        thoughtDefToGain = ThoughtDefOf.PrisonerBanishedToDie;
                    }
                    else
                    {
                        // Adjust for traits concerning prisoner released dangerously.
                        // Bloodlust trait has higher priority.
                        if (recipient.story.traits.HasTrait(TraitDefOf.Bloodlust))
                        {
                            thoughtDefToGain = Desynchronized_ThoughtDefOf.PrisonerReleasedDangerously_Bloodlust;
                        }
                        else if (recipient.story.traits.HasTrait(TraitDefOf.Psychopath))
                        {
                            thoughtDefToGain = Desynchronized_ThoughtDefOf.PrisonerReleasedDangerously_Psychopath;
                        }
                        else
                        {
                            thoughtDefToGain = Desynchronized_ThoughtDefOf.PrisonerReleasedDangerously;
                        }
                    }
                }

                recipient.needs.mood.thoughts.memories.TryGainMemory(thoughtDefToGain, BanishmentVictim);
            }
        }
    }
}
