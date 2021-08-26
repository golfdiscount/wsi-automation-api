"""
Contains middleware to handle requests to the ShipStation API

Currently supported GET endpoints: /orders, /products
Currently supported PUT endpoints: /products
"""

import requests
import base64


# noinspection SpellCheckingInspection
class Requester:
    _key, _domain, _hostname = "", "", ""

    def __init__(self, domain, hostname=None):
        """
        Initialize a Requester object to mediate GET and PUT requests
        and sets the server and hostname for the API connection

        @type domain: String
        @type hostname: String
        @param domain: The domain name of the API
        @param hostname: Hostname of the API
        """
        self._domain = domain
        self._hostname = hostname

    def get(self, endpoint, params=None, headers=None):
        """
        Submits a get request to a specific endpoint

        @type endpoint: String
        @param endpoint: The endpoint of the API to query
        @type params: dict
        @param params: Parameters to be submitted to the get request
        @type headers: dict
        @param headers: A collection of headers for this get request, defaults to None
        @rtype: dict
        @return: Returns the JSON response from the server
        @raise ValueError: The authentication provided is incorrect
        """
        url = self._domain + endpoint

        if headers is None:
            headers = {"Authorization": "Basic " + self._key}
        if params is None:
            params = {}

        res = requests.get(url, headers=headers, params=params)

        if res.status_code == 401:
            print(res.text)
            raise ValueError("Authentication is incorrect, please check username, password, and encoding method")

        assert res.status_code == 200, f"Error: {res.text}"

        return res.json()

    def post(self, endpoint, body, headers=None):
        """
        Submits a post request to the specified endpoint

        @type endpoint: String
        @param endpoint: The endpoint to submit the post request to
        @type body: dict
        @param body: The body of the post request
        @type headers: dict
        @param headers: A colleection of headers for this request, defaults to None
        @rtype: int
        @return: The status code from the server
        @raise ValueError: The submitted request has no post body
        """
        if body is None:
            raise ValueError("POST body cannot not be empty")
        if headers is None:
            headers = {}

        url = self._domain + endpoint

        res = requests.post(url, headers=headers, json=body)
        check_response(res)

        return res.json()

    def encode_base64(self, username, password):
        """
        Encodes a username and password for API authentication in base64

        @type username: String
        @param username: The username of the authentication header
        @type password: String
        @param password: The username of the authentication header
        """
        auth = username + ':' + password
        auth_bytes = auth.encode('ascii')
        auth_base64 = base64.b64encode(auth_bytes)

        self._key = auth_base64.decode('ascii')


def check_response(res):
    if int(res.status_code / 100) == 4:
        print(res.json())
        raise RuntimeError('A bad request was made')
    elif int(res.status_code / 100) == 5:
        print(res.json())
        raise RuntimeError('Internal server error')
    elif int(res.status_code / 100) != 2:
        print(res.json())
        raise RuntimeError('There was an error processing your request')
