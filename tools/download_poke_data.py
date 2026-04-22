import os
import requests
import json
import time
import re

BASE_DIR = "/Users/nergy/lt-mcb-NT106.Q22.ANTT/Server/wwwroot/data"
POKEMON_FRONT = os.path.join(BASE_DIR, "pokemon/front")
POKEMON_BACK = os.path.join(BASE_DIR, "pokemon/back")
POKEMON_ICONS = os.path.join(BASE_DIR, "pokemon/icons")
MOVES_DIR = os.path.join(BASE_DIR, "moves")

def slugify(text):
    return text.lower().replace(' ', '-').replace('.', '')

def download_file(url, path):
    try:
        response = requests.get(url, timeout=10)
        if response.status_code == 200:
            with open(path, 'wb') as f:
                f.write(response.content)
            return True
    except Exception as e:
        print(f"Error downloading {url}: {e}")
    return False

def get_pokemon_name(id):
    try:
        r = requests.get(f"https://pokeapi.co/api/v2/pokemon-species/{id}")
        if r.status_code == 200:
            return r.json()['name']
    except:
        pass
    return None

def get_move_name(id):
    try:
        r = requests.get(f"https://pokeapi.co/api/v2/move/{id}")
        if r.status_code == 200:
            return r.json()['name']
    except:
        pass
    return None

def download_data():
    # We'll fetch the list of all pokemon and moves first to get names efficiently
    print("Fetching Pokemon list...")
    poke_r = requests.get("https://pokeapi.co/api/v2/pokemon-species?limit=1100")
    pokemon_list = poke_r.json()['results'] if poke_r.status_code == 200 else []

    print("Fetching Move list...")
    move_r = requests.get("https://pokeapi.co/api/v2/move?limit=1000")
    move_list = move_r.json()['results'] if move_r.status_code == 200 else []

    print(f"Found {len(pokemon_list)} Pokemon and {len(move_list)} Moves.")

    # Download Pokemon Sprites with names
    for i, p in enumerate(pokemon_list):
        name = p['name']
        dex_id = i + 1
        
        # Front
        front_url = f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{dex_id}.png"
        download_file(front_url, os.path.join(POKEMON_FRONT, f"{name}.png"))
        
        # Back
        back_url = f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/back/{dex_id}.png"
        download_file(back_url, os.path.join(POKEMON_BACK, f"{name}.png"))
        
        # Icon
        icon_url = f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{dex_id}.png"
        download_file(icon_url, os.path.join(POKEMON_ICONS, f"{name}.png"))
        
        if dex_id % 50 == 0:
            print(f"Progress: {dex_id} Pokemon sprites downloaded as names.")

    # Download Move Data with names
    for i, m in enumerate(move_list):
        name = m['name']
        move_id = i + 1
        
        path = os.path.join(MOVES_DIR, f"{name}.json")
        if os.path.exists(path):
            continue
            
        url = f"https://pokeapi.co/api/v2/move/{move_id}/"
        try:
            r = requests.get(url, timeout=10)
            if r.status_code == 200:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(r.text)
            if move_id % 50 == 0:
                print(f"Progress: {move_id} Moves downloaded as names.")
        except:
            pass
        time.sleep(0.01)

if __name__ == "__main__":
    os.makedirs(POKEMON_FRONT, exist_ok=True)
    os.makedirs(POKEMON_BACK, exist_ok=True)
    os.makedirs(POKEMON_ICONS, exist_ok=True)
    os.makedirs(MOVES_DIR, exist_ok=True)
    
    download_data()
    print("Download complete!")
