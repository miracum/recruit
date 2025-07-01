FROM docker.io/library/python:3.13.5-slim-bookworm@sha256:d8130429ac8488cd869c7e7710e733aedc87bdd0d4bf816af831925c74b86e3f
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
