FROM docker.io/library/python:3.11.3-alpine@sha256:507818d46649f8543e58d19a00e3a1a428bb7e87c0bf7f7d1ffe7b076cda11be
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
