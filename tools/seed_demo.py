#!/usr/bin/env python3
"""
Dati dimostrativi per provare CardExchange (anche l'anteprima /preview) su un ambiente di sviluppo.

Crea 7 collezionisti in zone diverse di Milano, importa carte reali dai cataloghi pubblici
(Magic, Lorcana, Yu-Gi-Oh!, Pokémon se l'API risponde) e le distribuisce con qualche prezzo indicato
e qualche wishlist. Usa solo l'API pubblica: niente accesso diretto al database.

Uso (con il backend avviato):
    python tools/seed_demo.py                       # API su http://localhost:8080
    python tools/seed_demo.py --api http://localhost:5000

Poi accedi come "demo" con la password stampata alla fine. Rieseguirlo non duplica gli utenti
(se esistono già fa il login), ma aggiunge altre copie delle carte.
Solo per sviluppo: non usarlo su un database di produzione.
"""
import argparse
import json
import random
import sys
import urllib.error
import urllib.parse
import urllib.request

PASSWORD = "Demo1234!"

# Zone di Milano (coordinate indicative di quartiere)
USERS = [
    ("demo", "Demo", "Milano", 45.4642, 9.1900),        # Duomo: l'utente con cui accedere
    ("giulia.tcg", "Giulia", "Milano", 45.4520, 9.1750),  # Navigli
    ("marco_cards", "Marco", "Milano", 45.4780, 9.2270),  # Città Studi
    ("sara.collect", "Sara", "Milano", 45.4870, 9.1890),  # Isola
    ("luca-duel", "Luca", "Sesto San Giovanni", 45.5340, 9.2300),
    ("anna.lorcana", "Anna", "Monza", 45.5845, 9.2744),
    ("pietro.mtg", "Pietro", "Rho", 45.5280, 9.0400),
]

MAGIC = ["Lightning Bolt", "Sol Ring", "Counterspell", "Llanowar Elves", "Thoughtseize", "Sheoldred, the Apocalypse",
         "Ragavan, Nimble Pilferer", "The One Ring", "Swords to Plowshares", "Dark Ritual"]
LORCANA = ["Elsa", "Stitch", "Mickey Mouse", "Maleficent", "Moana", "Genie"]
YUGIOH = [46986414, 89631139, 55144522, 14558127, 44095762, 38033121]
POKEMON = ["Charizard", "Pikachu", "Mewtwo", "Gengar"]


class Api:
    def __init__(self, base):
        self.base = base.rstrip("/")
        self.token = None

    def call(self, method, path, body=None, timeout=60):
        data = json.dumps(body).encode() if body is not None else None
        headers = {"Content-Type": "application/json"}
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        req = urllib.request.Request(self.base + path, method=method, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                text = r.read().decode()
                return r.status, (json.loads(text) if text else None)
        except urllib.error.HTTPError as e:
            text = e.read().decode()
            try:
                return e.code, json.loads(text)
            except ValueError:
                return e.code, text


def login_or_register(api, username, first, city, lat, lng):
    status, res = api.call("POST", "/api/auth/login", {"usernameOrEmail": username, "password": PASSWORD})
    if status != 200:
        status, res = api.call("POST", "/api/auth/register", {
            "email": f"{username.replace('.', '').replace('-', '').replace('_', '')}@demo.cardexchange.local",
            "username": username, "firstName": first, "lastName": "Demo",
            "password": PASSWORD, "confirmPassword": PASSWORD, "birthDate": "1995-05-15", "acceptTerms": True})
        if status != 200:
            sys.exit(f"Registrazione di {username} non riuscita ({status}): {res}")
    api.token = res["accessToken"]
    user = res["user"]
    api.call("PUT", f"/api/users/{user['id']}/location", {
        "city": city, "province": "MI" if city != "Monza" else "MB", "country": "IT",
        "latitude": lat, "longitude": lng, "maxDistanceKm": 50})
    return user


def import_catalog(api):
    infos = []
    for name in MAGIC:
        s, r = api.call("POST", "/api/scryfall/import", {"name": name})
        if s in (200, 201) and r:
            infos.append(r.get("cardInfoId") or r.get("id"))
    for passcode in YUGIOH:
        s, r = api.call("POST", "/api/yugioh/import", {"yuGiOhId": passcode})
        if s in (200, 201) and r:
            infos.append(r.get("cardInfoId"))
    for key, names in (("lorcana", LORCANA), ("pokemon", POKEMON)):
        for name in names:
            s, r = api.call("GET", f"/api/catalog/{key}/search?q={urllib.parse.quote(name)}&limit=3")
            if s != 200:
                continue
            for card in (r.get("cards") or [])[:2]:
                s2, r2 = api.call("POST", f"/api/catalog/{key}/import", {"externalId": card["externalId"]})
                if s2 == 200:
                    infos.append(r2["cardInfoId"])
    return [i for i in dict.fromkeys(infos) if i]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default="http://localhost:8080")
    parser.add_argument("--seed", type=int, default=7)
    args = parser.parse_args()
    random.seed(args.seed)
    api = Api(args.api)

    users = {}
    for username, first, city, lat, lng in USERS:
        users[username] = (login_or_register(api, username, first, city, lat, lng), api.token)
        print(f"utente {username:14s} pronto ({city})")

    api.token = users["demo"][1]
    print("importo le carte dai cataloghi (può richiedere un minuto)…")
    infos = import_catalog(api)
    if not infos:
        sys.exit("Nessuna carta importata: controlla che il backend raggiunga i cataloghi online.")
    print(f"{len(infos)} carte nel catalogo locale")

    conditions = [1, 2, 2, 2, 3, 3, 4, 5]
    for username, (user, token) in users.items():
        api.token = token
        own = random.sample(infos, k=min(len(infos), random.randint(7, 11)))
        for info in own:
            body = {"cardInfoId": info, "condition": random.choice(conditions), "quantity": random.choice([1, 1, 1, 2]),
                    "isAvailableForTrade": random.random() < 0.85}
            if random.random() < 0.35:
                body["askingPrice"] = round(random.uniform(1, 40), 2)
            api.call("POST", f"/api/cards/user/{user['id']}", body)
        wanted = random.sample([i for i in infos if i not in own], k=min(5, len(infos) - len(own)))
        for info in wanted:
            api.call("POST", f"/api/wishlist/user/{user['id']}", {"cardInfoId": info, "priority": random.choice([1, 2, 3])})
        print(f"{username:14s}: {len(own)} carte, {len(wanted)} in wishlist")

    print(f"\nFatto. Accedi come  demo / {PASSWORD}  e apri /preview")


if __name__ == "__main__":
    main()
