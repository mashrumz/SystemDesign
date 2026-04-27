using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Shared.Data;

/// <summary>
/// Seeds reference data (FCs, items, inventory) if the tables are empty.
/// Safe to call from every service replica on startup — skips if already seeded.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(GoPuffDbContext db)
    {
        if (await db.FulfillmentCentres.AnyAsync()) return;

        // Fulfilment Centres — NYC metro, Chicago, Los Angeles
        var fcs = new[]
        {
            new FulfillmentCentre { Id = 1, Name = "Manhattan FC",         Lat =  40.7580, Lon =  -73.9855 },
            new FulfillmentCentre { Id = 2, Name = "Brooklyn FC",          Lat =  40.6782, Lon =  -73.9442 },
            new FulfillmentCentre { Id = 3, Name = "Queens FC",            Lat =  40.7282, Lon =  -73.7949 },
            new FulfillmentCentre { Id = 4, Name = "Bronx FC",             Lat =  40.8448, Lon =  -73.8648 },
            new FulfillmentCentre { Id = 5, Name = "Chicago Loop FC",      Lat =  41.8827, Lon =  -87.6233 },
            new FulfillmentCentre { Id = 6, Name = "Chicago Lincoln Park", Lat =  41.9216, Lon =  -87.6533 },
            new FulfillmentCentre { Id = 7, Name = "LA Downtown FC",       Lat =  34.0522, Lon = -118.2437 },
            new FulfillmentCentre { Id = 8, Name = "LA Santa Monica FC",   Lat =  34.0195, Lon = -118.4912 },
        };

        // Advance the sequence so new inserts won't collide with seeded IDs
        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('fulfillment_centres','id'), 100)");
        db.FulfillmentCentres.AddRange(fcs);

        // Items
        var items = new[]
        {
            new Item { Id =  1, Name = "Water Bottle (1L)" },
            new Item { Id =  2, Name = "Chips (Lay's Original)" },
            new Item { Id =  3, Name = "Red Bull (250ml)" },
            new Item { Id =  4, Name = "Beer (Heineken 6-pack)" },
            new Item { Id =  5, Name = "Tylenol Extra Strength" },
            new Item { Id =  6, Name = "White Bread Loaf" },
            new Item { Id =  7, Name = "Eggs (12-pack)" },
            new Item { Id =  8, Name = "Milk (1 gallon)" },
            new Item { Id =  9, Name = "Ice Cream (Ben & Jerry's)" },
            new Item { Id = 10, Name = "Cup Noodles Ramen" },
        };

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('items','id'), 100)");
        db.Items.AddRange(items);

        await db.SaveChangesAsync();

        // Inventory (itemId, fcId, quantity)
        var inv = new List<Inventory>
        {
            // Manhattan (1) — full range
            new() { ItemId=1, FcId=1, Quantity=500 }, new() { ItemId=2, FcId=1, Quantity=200 },
            new() { ItemId=3, FcId=1, Quantity=300 }, new() { ItemId=4, FcId=1, Quantity=50  },
            new() { ItemId=5, FcId=1, Quantity=100 }, new() { ItemId=6, FcId=1, Quantity=80  },
            new() { ItemId=7, FcId=1, Quantity=60  }, new() { ItemId=8, FcId=1, Quantity=40  },
            new() { ItemId=9, FcId=1, Quantity=30  }, new() { ItemId=10,FcId=1, Quantity=150 },
            // Brooklyn (2)
            new() { ItemId=1, FcId=2, Quantity=400 }, new() { ItemId=2, FcId=2, Quantity=180 },
            new() { ItemId=3, FcId=2, Quantity=250 }, new() { ItemId=4, FcId=2, Quantity=40  },
            new() { ItemId=5, FcId=2, Quantity=80  }, new() { ItemId=6, FcId=2, Quantity=70  },
            new() { ItemId=7, FcId=2, Quantity=50  }, new() { ItemId=8, FcId=2, Quantity=35  },
            new() { ItemId=9, FcId=2, Quantity=20  }, new() { ItemId=10,FcId=2, Quantity=120 },
            // Queens (3) — missing items 3, 9
            new() { ItemId=1, FcId=3, Quantity=300 }, new() { ItemId=2, FcId=3, Quantity=150 },
            new() { ItemId=4, FcId=3, Quantity=30  }, new() { ItemId=5, FcId=3, Quantity=60  },
            new() { ItemId=6, FcId=3, Quantity=90  }, new() { ItemId=7, FcId=3, Quantity=70  },
            new() { ItemId=8, FcId=3, Quantity=50  }, new() { ItemId=10,FcId=3, Quantity=90  },
            // Bronx (4)
            new() { ItemId=1, FcId=4, Quantity=200 }, new() { ItemId=2, FcId=4, Quantity=100 },
            new() { ItemId=3, FcId=4, Quantity=100 }, new() { ItemId=5, FcId=4, Quantity=40  },
            new() { ItemId=6, FcId=4, Quantity=50  }, new() { ItemId=7, FcId=4, Quantity=40  },
            new() { ItemId=9, FcId=4, Quantity=10  }, new() { ItemId=10,FcId=4, Quantity=80  },
            // Chicago Loop (5)
            new() { ItemId=1, FcId=5, Quantity=450 }, new() { ItemId=2, FcId=5, Quantity=200 },
            new() { ItemId=3, FcId=5, Quantity=280 }, new() { ItemId=4, FcId=5, Quantity=60  },
            new() { ItemId=5, FcId=5, Quantity=90  }, new() { ItemId=6, FcId=5, Quantity=75  },
            new() { ItemId=7, FcId=5, Quantity=55  }, new() { ItemId=8, FcId=5, Quantity=45  },
            new() { ItemId=9, FcId=5, Quantity=25  }, new() { ItemId=10,FcId=5, Quantity=140 },
            // Chicago Lincoln Park (6)
            new() { ItemId=1, FcId=6, Quantity=350 }, new() { ItemId=2, FcId=6, Quantity=160 },
            new() { ItemId=4, FcId=6, Quantity=45  }, new() { ItemId=5, FcId=6, Quantity=70  },
            new() { ItemId=6, FcId=6, Quantity=65  }, new() { ItemId=7, FcId=6, Quantity=48  },
            new() { ItemId=8, FcId=6, Quantity=38  }, new() { ItemId=9, FcId=6, Quantity=18  },
            new() { ItemId=10,FcId=6, Quantity=110 },
            // LA Downtown (7)
            new() { ItemId=1, FcId=7, Quantity=480 }, new() { ItemId=2, FcId=7, Quantity=210 },
            new() { ItemId=3, FcId=7, Quantity=310 }, new() { ItemId=4, FcId=7, Quantity=55  },
            new() { ItemId=5, FcId=7, Quantity=95  }, new() { ItemId=6, FcId=7, Quantity=82  },
            new() { ItemId=7, FcId=7, Quantity=62  }, new() { ItemId=8, FcId=7, Quantity=42  },
            new() { ItemId=9, FcId=7, Quantity=32  }, new() { ItemId=10,FcId=7, Quantity=160 },
            // LA Santa Monica (8)
            new() { ItemId=1, FcId=8, Quantity=320 }, new() { ItemId=2, FcId=8, Quantity=140 },
            new() { ItemId=3, FcId=8, Quantity=190 }, new() { ItemId=4, FcId=8, Quantity=35  },
            new() { ItemId=5, FcId=8, Quantity=65  }, new() { ItemId=6, FcId=8, Quantity=55  },
            new() { ItemId=8, FcId=8, Quantity=30  }, new() { ItemId=9, FcId=8, Quantity=22  },
            new() { ItemId=10,FcId=8, Quantity=100 },
        };

        db.Inventories.AddRange(inv);
        await db.SaveChangesAsync();
    }
}
