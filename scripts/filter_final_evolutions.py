import os
import requests
import json
from pymongo import MongoClient

# Configuration
MONGO_URI = "mongodb://localhost:27017"
DB_NAME = "pokemon_mmo"

def get_leaves(chain_link, leaves):
    if not chain_link['evolves_to']:
        leaves.add(chain_link['species']['name'])
        return
    for evolution in chain_link['evolves_to']:
        get_leaves(evolution, leaves)

def filter_final_forms():
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    
    print("Fetching evolution chains...")
    # There are about 550 evolution chains total
    r = requests.get("https://pokeapi.co/api/v2/evolution-chain?limit=600").json()['results']
    
    final_forms = set()
    final_forms.add("pikachu") # Keep Pikachu
    
    for i, chain_info in enumerate(r):
        try:
            chain_data = requests.get(chain_info['url']).json()
            get_leaves(chain_data['chain'], final_forms)
        except:
            pass
        if (i+1) % 50 == 0:
            print(f"  Processed {i+1} evolution chains...")

    print(f"Total final forms identified: {len(final_forms)}")

    # Get the capitalization right for MongoDB
    names_to_keep = [n.capitalize() for n in final_forms]
    
    # Special case: some names in PokeAPI have dashes or special logic
    # But usually capitalize() works for the basic ones.
    
    # Delete non-final forms
    result = db["Pokedex"].delete_many({"Name": {"$nin": names_to_keep}})
    print(f"Deleted {result.deleted_count} non-final evolution entries.")
    
    # Optional: Keep Pikachu even if it's not a leaf (Pikachu evolves to Raichu)
    # The 'leaves' algorithm above would normally skip Pikachu because it evolves to Raichu.
    # But I added it manually.

if __name__ == "__main__":
    filter_final_forms()
