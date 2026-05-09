import os
import json
from pymongo import MongoClient

# Configuration
MONGO_URI = "mongodb://localhost:27017"
DB_NAME = "pokemon_mmo"
MOVES_DIR = "/Users/nergy/lt-mcb-NT106.Q22.ANTT/Server/wwwroot/data/moves"

def seed_moves():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    moves_col = db["Moves"]

    files = [f for f in os.listdir(MOVES_DIR) if f.endswith('.json')]
    print(f"Found {len(files)} move files in {MOVES_DIR}")

    count = 0
    for filename in files:
        path = os.path.join(MOVES_DIR, filename)
        try:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Map PokeAPI JSON to MoveEntry model
            # MoveEntry.cs likely has: Id, Name, Power, Accuracy, Type, Priority, Category, PP
            move_id = data.get('id')
            if move_id is None:
                continue
            
            # category: physical, special, status
            damage_class = data.get('damage_class', {}).get('name', 'status')
            
            move_doc = {
                "Id": move_id,
                "Name": data.get('name', f"Move#{move_id}").replace('-', ' ').title(),
                "Power": data.get('power'),
                "Accuracy": data.get('accuracy'),
                "Type": data.get('type', {}).get('name', 'normal'),
                "Priority": data.get('priority', 0),
                "Category": damage_class,
                "PP": data.get('pp', 10)
            }
            
            # Upsert
            moves_col.update_one({"Id": move_id}, {"$set": move_doc}, upsert=True)
            count += 1
            if count % 100 == 0:
                print(f"Seeded {count} moves...")
                
        except Exception as e:
            print(f"Error processing {filename}: {e}")

    print(f"Finished seeding {count} moves to MongoDB.")

if __name__ == "__main__":
    if not os.path.exists(MOVES_DIR):
        print(f"Directory {MOVES_DIR} does not exist yet. Please wait for the download script to finish.")
    else:
        seed_moves()
