FROM docker.io/library/python:3.12.0-alpine@sha256:3d2969ec27f0b38fc53ba7e4b6e01459a6f397f27649d82dc8e82631267bca0d
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
