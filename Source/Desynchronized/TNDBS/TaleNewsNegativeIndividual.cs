﻿using Desynchronized.TNDBS.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Desynchronized.TNDBS
{
    public abstract class TaleNewsNegativeIndividual : TaleNews
    {
        private Pawn primaryVictim;
        protected InstigatorInfo instigatorInfo;

        public Pawn PrimaryVictim
        {
            get
            {
                return primaryVictim;
            }
        }

        public InstigatorInfo InstigatorInfo
        {
            get
            {
                return instigatorInfo;
            }
            protected set
            {
                instigatorInfo = value;
            }
        }

        /// <summary>
        /// The (primary) instigator of this negative tale-news, if there exists one.
        /// </summary>
        public Pawn Instigator
        {
            get
            {
                return InstigatorInfo.Instigator;
            }
        }

        public TaleNewsNegativeIndividual()
        {

        }

        public TaleNewsNegativeIndividual(Pawn victim, InstigatorInfo instigInfo): base(new LocationInfo(victim.MapHeld, victim.PositionHeld))
        {
            primaryVictim = victim;
            InstigatorInfo = instigInfo;
        }

        public static TaleNewsNegativeIndividual GenerateTaleNewsNegativeIndividual(TaleNewsTypeEnum typeEnum, Pawn primaryVictim, InstigatorInfo instigatorInfo)
        {
            TaleNewsNegativeIndividual taleNews = GenerateTaleNewsGenerally(typeEnum) as TaleNewsNegativeIndividual;
            taleNews.primaryVictim = primaryVictim;
            taleNews.instigatorInfo = instigatorInfo;
            return taleNews;
        }

        protected override void ConductSaveFileIO()
        {
            Scribe_References.Look(ref primaryVictim, "primaryVictim", true);
            Scribe_Deep.Look(ref instigatorInfo, "instigatorInfo");
        }

        public override bool PawnIsInvolved(Pawn pawn)
        {
            if (PrimaryVictim != null && pawn == PrimaryVictim)
            {
                return true;
            }
            if (Instigator != null && pawn == Instigator)
            {
                return true;
            }

            return false;
        }

        public override bool IsValid()
        {
            return (PrimaryVictim != null);
        }

        internal override void SelfVerify()
        {
            if (LocationOfOccurence == null)
            {
                LocationOfOccurence = LocationInfo.EmptyLocationInfo;
            }
        }
    }
}
