﻿using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class QueuedPlayer
    {
        [ForeignKey("GuildId")]
        public Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        [ForeignKey("ChannelId")]
        public Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }
    }
}