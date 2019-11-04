﻿using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class PartyMember
    {
        public ulong PartyHost { get; set; }

        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
    }
}