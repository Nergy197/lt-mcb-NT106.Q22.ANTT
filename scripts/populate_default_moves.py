import os
import json
import re
from pymongo import MongoClient

# Configuration
MONGO_URI = "mongodb://localhost:27017"
DB_NAME = "pokemon_mmo"
BASE_DIR = "/Users/nergy/lt-mcb-NT106.Q22.ANTT/Server/wwwroot/data"
SPECIES_DIR = os.path.join(BASE_DIR, "species")

def get_id_from_url(url):
    match = re.search(r'/(\d+)/$', url)
    return int(match.group(1)) if match else 0

def populate_moves():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    
    files = [f for f in os.listdir(SPECIES_DIR) if f.endswith('.json')]
    print(f"Processing {len(files)} species files for movesets...")
    
    for filename in files:
        path = os.path.join(SPECIES_DIR, filename)
        with open(path, 'r', encoding='utf-8') as f:
            try:
                data = json.load(f)
            except:
                continue
        
        dex_id = data.get('id')
        if not dex_id: continue
        
        # Extract Level-up moves
        level_up_moves = []
        for m in data.get('moves', []):
            move_id = get_id_from_url(m['move']['url'])
            # We look for the lowest level_learned_at across all versions
            min_level = 999
            is_levelup = False
            for detail in m.get('version_group_details', []):
                if detail['move_learn_method']['name'] == 'level-up':
                    is_levelup = True
                    level = detail['level_learned_at']
                    if level < min_level:
                        min_level = level
            
            if is_levelup:
                level_up_moves.append({'id': move_id, 'level': min_level})
        
        # Sort by level and take top 4
        level_up_moves.sort(key=lambda x: x['level'])
        default_move_ids = [m['id'] for m in level_up_moves[:4]]
        
        # Update MongoDB
        db["Pokedex"].update_one(
            {"Id": dex_id},
            {"$set": {"DefaultMoves": default_move_ids}}
        )

    print("Moveset population complete!")

if __name__ == "__main__":
    populate_moves()
