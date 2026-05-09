import os
import requests
import json
import time
from pymongo import MongoClient

# Configuration
MONGO_URI = "mongodb://localhost:27017"
DB_NAME = "pokemon_mmo"
BASE_DIR = "/Users/nergy/lt-mcb-NT106.Q22.ANTT/Server/wwwroot/data"
POKEMON_FRONT = os.path.join(BASE_DIR, "pokemon/front")
POKEMON_BACK = os.path.join(BASE_DIR, "pokemon/back")
POKEMON_ICONS = os.path.join(BASE_DIR, "pokemon/icons")
MOVES_DIR = os.path.join(BASE_DIR, "moves")
SPECIES_DIR = os.path.join(BASE_DIR, "species") # New: raw species data for seeding

def download_file(url, path):
    if os.path.exists(path): return True
    try:
        r = requests.get(url, timeout=10)
        if r.status_code == 200:
            with open(path, 'wb') as f:
                f.write(r.content)
            return True
    except: pass
    return False

def sync_data():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    
    # 1. Fetch Lists
    print("Fetching PokeAPI metadata...")
    poke_r = requests.get("https://pokeapi.co/api/v2/pokemon-species?limit=1100").json()['results']
    move_r = requests.get("https://pokeapi.co/api/v2/move?limit=1000").json()['results']
    
    # 2. Download & Seed Moves
    print(f"Syncing {len(move_r)} Moves...")
    for i, m in enumerate(move_r):
        name = m['name']
        move_id = i + 1
        path = os.path.join(MOVES_DIR, f"{name}.json")
        
        if not os.path.exists(path):
            r = requests.get(f"https://pokeapi.co/api/v2/move/{move_id}/")
            if r.status_code == 200:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(r.text)
                data = r.json()
            else: continue
        else:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
        
        # Seed to DB
        move_doc = {
            "Id": move_id,
            "Name": data.get('name', "").replace('-', ' ').title(),
            "Power": data.get('power'),
            "Accuracy": data.get('accuracy'),
            "Type": data.get('type', {}).get('name', 'normal'),
            "Priority": data.get('priority', 0),
            "Category": data.get('damage_class', {}).get('name', 'physical'),
            "PP": data.get('pp', 10)
        }
        db["Moves"].update_one({"Id": move_id}, {"$set": move_doc}, upsert=True)
        if (i+1) % 100 == 0: print(f"  Processed {i+1} moves...")

    # 3. Download & Seed Pokedex
    print(f"Syncing {len(poke_r)} Pokemon Species...")
    for i, p in enumerate(poke_r):
        name = p['name']
        dex_id = i + 1
        
        # We need Stats, which are in /pokemon/{id}, not /pokemon-species/{id}
        # But we'll just fetch a basic set or the full pokemon data
        path = os.path.join(SPECIES_DIR, f"{name}.json")
        if not os.path.exists(path):
            r = requests.get(f"https://pokeapi.co/api/v2/pokemon/{dex_id}/")
            if r.status_code == 200:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(r.text)
                data = r.json()
            else: continue
        else:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)

        # Map to PokedexEntry
        stats = {s['stat']['name']: s['base_stat'] for s in data.get('stats', [])}
        types = [t['type']['name'] for t in data.get('types', [])]
        
        poke_doc = {
            "Id": dex_id,
            "Name": name.capitalize(),
            "Types": types,
            "BaseStats": stats,
            "Height": data.get('height', 0),
            "Weight": data.get('weight', 0),
            "SpriteUrl": f"/data/pokemon/front/{name}.png"
        }
        db["Pokedex"].update_one({"Id": dex_id}, {"$set": poke_doc}, upsert=True)
        
        # Trigger Sprite downloads (optional: background later)
        # For now, let's just make sure the directory is there
        if (i+1) % 100 == 0: print(f"  Processed {i+1} pokemon...")

    print("DB Sync Complete! Now starting background sprite download...")

if __name__ == "__main__":
    os.makedirs(POKEMON_FRONT, exist_ok=True)
    os.makedirs(POKEMON_BACK, exist_ok=True)
    os.makedirs(POKEMON_ICONS, exist_ok=True)
    os.makedirs(MOVES_DIR, exist_ok=True)
    os.makedirs(SPECIES_DIR, exist_ok=True)
    
    sync_data()
